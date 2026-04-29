namespace ERP.Core.CarteraFinanciera.Forms
{
    partial class FrmCupoTarj
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
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
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
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle DataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            this.DgwTarjetas = new System.Windows.Forms.DataGridView();
            this.ClmTarjeta = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmCupo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmDisponible = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmCupCajero = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmCupopos = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmClase = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ClmError = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CupoCredito = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DispCredDs = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CupoCajDs = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CupoPosDs = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.LblEmpresa = new System.Windows.Forms.Label();
            this.BtnSalir = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.LblAsociado = new System.Windows.Forms.Label();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.LblCodigoter = new System.Windows.Forms.Label();
            this.LblPeriodo = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.DgwTarjetas)).BeginInit();
            this.SuspendLayout();
            //
            // DgwTarjetas
            //
            this.DgwTarjetas.AllowUserToAddRows = false;
            this.DgwTarjetas.AllowUserToDeleteRows = false;
            this.DgwTarjetas.AllowUserToOrderColumns = true;
            this.DgwTarjetas.AllowUserToResizeColumns = false;
            this.DgwTarjetas.AllowUserToResizeRows = false;
            DataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            DataGridViewCellStyle1.BackColor = System.Drawing.Color.LightSlateGray;
            DataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.Window;
            DataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.DarkBlue;
            DataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwTarjetas.ColumnHeadersDefaultCellStyle = DataGridViewCellStyle1;
            this.DgwTarjetas.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgwTarjetas.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ClmTarjeta,
            this.ClmCupo,
            this.ClmDisponible,
            this.ClmCupCajero,
            this.ClmCupopos,
            this.ClmClase,
            this.ClmError,
            this.CupoCredito,
            this.DispCredDs,
            this.CupoCajDs,
            this.CupoPosDs});
            this.DgwTarjetas.EnableHeadersVisualStyles = false;
            this.DgwTarjetas.Location = new System.Drawing.Point(9, 99);
            this.DgwTarjetas.MultiSelect = false;
            this.DgwTarjetas.Name = "DgwTarjetas";
            this.DgwTarjetas.ReadOnly = true;
            DataGridViewCellStyle13.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle13.BackColor = System.Drawing.SystemColors.Control;
            DataGridViewCellStyle13.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            DataGridViewCellStyle13.ForeColor = System.Drawing.SystemColors.WindowText;
            DataGridViewCellStyle13.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle13.SelectionForeColor = System.Drawing.Color.DarkBlue;
            DataGridViewCellStyle13.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgwTarjetas.RowHeadersDefaultCellStyle = DataGridViewCellStyle13;
            this.DgwTarjetas.RowHeadersVisible = false;
            DataGridViewCellStyle14.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle14.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.DgwTarjetas.RowsDefaultCellStyle = DataGridViewCellStyle14;
            this.DgwTarjetas.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgwTarjetas.Size = new System.Drawing.Size(663, 123);
            this.DgwTarjetas.TabIndex = 0;
            this.DgwTarjetas.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgwTarjetas_CellContentClick);
            this.DgwTarjetas.DoubleClick += new System.EventHandler(this.DgwTarjetas_DoubleClick);
            //
            // ClmTarjeta
            //
            this.ClmTarjeta.DataPropertyName = "Tarjeta";
            DataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.ClmTarjeta.DefaultCellStyle = DataGridViewCellStyle2;
            this.ClmTarjeta.HeaderText = "Nro. Tarjeta";
            this.ClmTarjeta.Name = "ClmTarjeta";
            this.ClmTarjeta.ReadOnly = true;
            this.ClmTarjeta.Width = 130;
            //
            // ClmCupo
            //
            this.ClmCupo.DataPropertyName = "cupocredito";
            DataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle3.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle3.Format = "N2";
            DataGridViewCellStyle3.NullValue = "0";
            DataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.ClmCupo.DefaultCellStyle = DataGridViewCellStyle3;
            this.ClmCupo.HeaderText = "Cupo";
            this.ClmCupo.Name = "ClmCupo";
            this.ClmCupo.ReadOnly = true;
            //
            // ClmDisponible
            //
            this.ClmDisponible.DataPropertyName = "disponible";
            DataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle4.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle4.Format = "N2";
            DataGridViewCellStyle4.NullValue = "0";
            DataGridViewCellStyle4.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle4.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.ClmDisponible.DefaultCellStyle = DataGridViewCellStyle4;
            this.ClmDisponible.HeaderText = "Disponible";
            this.ClmDisponible.Name = "ClmDisponible";
            this.ClmDisponible.ReadOnly = true;
            //
            // ClmCupCajero
            //
            this.ClmCupCajero.DataPropertyName = "cupocajero";
            DataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle5.BackColor = System.Drawing.Color.Ivory;
            DataGridViewCellStyle5.Format = "N2";
            DataGridViewCellStyle5.NullValue = "0";
            DataGridViewCellStyle5.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle5.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.ClmCupCajero.DefaultCellStyle = DataGridViewCellStyle5;
            this.ClmCupCajero.HeaderText = "Cupo Cajero";
            this.ClmCupCajero.Name = "ClmCupCajero";
            this.ClmCupCajero.ReadOnly = true;
            //
            // ClmCupopos
            //
            this.ClmCupopos.DataPropertyName = "cupopos";
            DataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle6.BackColor = System.Drawing.Color.Beige;
            DataGridViewCellStyle6.Format = "N2";
            DataGridViewCellStyle6.NullValue = "0";
            DataGridViewCellStyle6.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle6.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.ClmCupopos.DefaultCellStyle = DataGridViewCellStyle6;
            this.ClmCupopos.HeaderText = "Cupo Pos";
            this.ClmCupopos.Name = "ClmCupopos";
            this.ClmCupopos.ReadOnly = true;
            //
            // ClmClase
            //
            this.ClmClase.DataPropertyName = "DebCre";
            DataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            DataGridViewCellStyle7.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle7.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.ClmClase.DefaultCellStyle = DataGridViewCellStyle7;
            this.ClmClase.HeaderText = "Clase";
            this.ClmClase.Name = "ClmClase";
            this.ClmClase.ReadOnly = true;
            this.ClmClase.Width = 50;
            //
            // ClmError
            //
            this.ClmError.DataPropertyName = "error";
            DataGridViewCellStyle8.SelectionBackColor = System.Drawing.Color.LightSteelBlue;
            DataGridViewCellStyle8.SelectionForeColor = System.Drawing.Color.DarkBlue;
            this.ClmError.DefaultCellStyle = DataGridViewCellStyle8;
            this.ClmError.HeaderText = "Estado";
            this.ClmError.Name = "ClmError";
            this.ClmError.ReadOnly = true;
            this.ClmError.Width = 60;
            //
            // CupoCredito
            //
            this.CupoCredito.DataPropertyName = "CupoCredito";
            DataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle9.Format = "N2";
            DataGridViewCellStyle9.NullValue = "0";
            this.CupoCredito.DefaultCellStyle = DataGridViewCellStyle9;
            this.CupoCredito.HeaderText = "Cupo Credito";
            this.CupoCredito.Name = "CupoCredito";
            this.CupoCredito.ReadOnly = true;
            //
            // DispCredDs
            //
            this.DispCredDs.DataPropertyName = "DispCredDs";
            DataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle10.Format = "N2";
            DataGridViewCellStyle10.NullValue = "0";
            this.DispCredDs.DefaultCellStyle = DataGridViewCellStyle10;
            this.DispCredDs.HeaderText = "Disponible DS";
            this.DispCredDs.Name = "DispCredDs";
            this.DispCredDs.ReadOnly = true;
            //
            // CupoCajDs
            //
            this.CupoCajDs.DataPropertyName = "CupoCajDs";
            DataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle11.Format = "N2";
            DataGridViewCellStyle11.NullValue = "0";
            this.CupoCajDs.DefaultCellStyle = DataGridViewCellStyle11;
            this.CupoCajDs.HeaderText = "Cupo Cajero DS";
            this.CupoCajDs.Name = "CupoCajDs";
            this.CupoCajDs.ReadOnly = true;
            this.CupoCajDs.Width = 110;
            //
            // CupoPosDs
            //
            this.CupoPosDs.DataPropertyName = "CupoPosDs";
            DataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            DataGridViewCellStyle12.Format = "N2";
            DataGridViewCellStyle12.NullValue = "0";
            this.CupoPosDs.DefaultCellStyle = DataGridViewCellStyle12;
            this.CupoPosDs.HeaderText = "Cupo Pos Ds";
            this.CupoPosDs.Name = "CupoPosDs";
            this.CupoPosDs.ReadOnly = true;
            //
            // LblEmpresa
            //
            this.LblEmpresa.BackColor = System.Drawing.Color.Gainsboro;
            this.LblEmpresa.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblEmpresa.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblEmpresa.Location = new System.Drawing.Point(96, 5);
            this.LblEmpresa.Name = "LblEmpresa";
            this.LblEmpresa.Size = new System.Drawing.Size(489, 40);
            this.LblEmpresa.TabIndex = 1;
            this.LblEmpresa.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // BtnSalir
            //
            this.BtnSalir.BackgroundImage = Properties.Resources.Salir031;
            this.BtnSalir.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.BtnSalir.Location = new System.Drawing.Point(626, 228);
            this.BtnSalir.Name = "BtnSalir";
            this.BtnSalir.Size = new System.Drawing.Size(38, 35);
            this.BtnSalir.TabIndex = 230;
            this.BtnSalir.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.ToolTip1.SetToolTip(this.BtnSalir, "Salir");
            this.BtnSalir.UseVisualStyleBackColor = true;
            this.BtnSalir.Click += new System.EventHandler(this.BtnSalir_Click);
            //
            // Label1
            //
            this.Label1.AutoSize = true;
            this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Label1.Location = new System.Drawing.Point(20, 67);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(69, 16);
            this.Label1.TabIndex = 231;
            this.Label1.Text = "Asociado:";
            //
            // LblAsociado
            //
            this.LblAsociado.BackColor = System.Drawing.Color.Gainsboro;
            this.LblAsociado.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblAsociado.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblAsociado.Location = new System.Drawing.Point(249, 64);
            this.LblAsociado.Name = "LblAsociado";
            this.LblAsociado.Size = new System.Drawing.Size(415, 23);
            this.LblAsociado.TabIndex = 232;
            //
            // LblCodigoter
            //
            this.LblCodigoter.BackColor = System.Drawing.Color.Gainsboro;
            this.LblCodigoter.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.LblCodigoter.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblCodigoter.Location = new System.Drawing.Point(95, 66);
            this.LblCodigoter.Name = "LblCodigoter";
            this.LblCodigoter.Size = new System.Drawing.Size(141, 23);
            this.LblCodigoter.TabIndex = 233;
            //
            // LblPeriodo
            //
            this.LblPeriodo.AutoSize = true;
            this.LblPeriodo.Location = new System.Drawing.Point(300, 249);
            this.LblPeriodo.Name = "LblPeriodo";
            this.LblPeriodo.Size = new System.Drawing.Size(57, 13);
            this.LblPeriodo.TabIndex = 234;
            this.LblPeriodo.Text = "LblPeriodo";
            this.LblPeriodo.Visible = false;
            //
            // FrmCupoTarj
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(680, 272);
            this.Controls.Add(this.LblPeriodo);
            this.Controls.Add(this.LblCodigoter);
            this.Controls.Add(this.LblAsociado);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.BtnSalir);
            this.Controls.Add(this.LblEmpresa);
            this.Controls.Add(this.DgwTarjetas);
            this.Name = "FrmCupoTarj";
            this.Text = "Cupo Tarjetas";
            this.Load += new System.EventHandler(this.FrmCupoTarj_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DgwTarjetas)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        internal System.Windows.Forms.DataGridView DgwTarjetas;
        internal System.Windows.Forms.Button BtnSalir;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.ToolTip ToolTip1;
        public System.Windows.Forms.Label LblEmpresa;
        public System.Windows.Forms.Label LblCodigoter;
        internal System.Windows.Forms.Label LblAsociado;
        public System.Windows.Forms.Label LblPeriodo;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmTarjeta;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmCupo;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmDisponible;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmCupCajero;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmCupopos;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmClase;
        internal System.Windows.Forms.DataGridViewTextBoxColumn ClmError;
        internal System.Windows.Forms.DataGridViewTextBoxColumn CupoCredito;
        internal System.Windows.Forms.DataGridViewTextBoxColumn DispCredDs;
        internal System.Windows.Forms.DataGridViewTextBoxColumn CupoCajDs;
        internal System.Windows.Forms.DataGridViewTextBoxColumn CupoPosDs;
    }
}
