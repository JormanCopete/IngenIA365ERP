namespace ERP.Core.Compartido.Forms
{
    partial class FrmAyuDocs
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            this.DtgDocs = new System.Windows.Forms.DataGridView();
            this.txtCpte = new System.Windows.Forms.TextBox();
            this.txtConse = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.CmdBuscar = new System.Windows.Forms.Button();
            this.Label4 = new System.Windows.Forms.Label();
            this.DtpFecini = new System.Windows.Forms.DateTimePicker();
            this.Label2 = new System.Windows.Forms.Label();
            this.DtpFecFin = new System.Windows.Forms.DateTimePicker();
            ((System.ComponentModel.ISupportInitialize)(this.DtgDocs)).BeginInit();
            this.SuspendLayout();
            //
            // DtgDocs
            //
            this.DtgDocs.AllowUserToAddRows = false;
            this.DtgDocs.AllowUserToDeleteRows = false;
            this.DtgDocs.AllowUserToResizeColumns = false;
            this.DtgDocs.AllowUserToResizeRows = false;
            this.DtgDocs.BackgroundColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.DtgDocs.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            dataGridViewCellStyle13.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle13.BackColor = System.Drawing.Color.DarkCyan;
            dataGridViewCellStyle13.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            dataGridViewCellStyle13.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle13.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle13.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle13.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DtgDocs.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle13;
            this.DtgDocs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DtgDocs.GridColor = System.Drawing.SystemColors.AppWorkspace;
            this.DtgDocs.Location = new System.Drawing.Point(2, 50);
            this.DtgDocs.MultiSelect = false;
            this.DtgDocs.Name = "DtgDocs";
            this.DtgDocs.ReadOnly = true;
            this.DtgDocs.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Sunken;
            this.DtgDocs.RowHeadersVisible = false;
            dataGridViewCellStyle14.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, (byte)0);
            this.DtgDocs.RowsDefaultCellStyle = dataGridViewCellStyle14;
            this.DtgDocs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DtgDocs.Size = new System.Drawing.Size(566, 243);
            this.DtgDocs.StandardTab = true;
            this.DtgDocs.TabIndex = 5;
            this.DtgDocs.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DtgDocs_CellContentClick);
            this.DtgDocs.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.DtgDocs_CellMouseDoubleClick);
            //
            // txtCpte
            //
            this.txtCpte.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.txtCpte.Enabled = false;
            this.txtCpte.Location = new System.Drawing.Point(81, 13);
            this.txtCpte.Margin = new System.Windows.Forms.Padding(4);
            this.txtCpte.MaxLength = 4;
            this.txtCpte.Name = "txtCpte";
            this.txtCpte.Size = new System.Drawing.Size(41, 20);
            this.txtCpte.TabIndex = 0;
            //
            // txtConse
            //
            this.txtConse.AllowDrop = true;
            this.txtConse.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.txtConse.Location = new System.Drawing.Point(130, 13);
            this.txtConse.Margin = new System.Windows.Forms.Padding(4);
            this.txtConse.MaxLength = 8;
            this.txtConse.Name = "txtConse";
            this.txtConse.Size = new System.Drawing.Size(72, 20);
            this.txtConse.TabIndex = 1;
            this.txtConse.Text = "0";
            this.txtConse.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtConse.TextChanged += new System.EventHandler(this.txtConse_TextChanged);
            //
            // Label1
            //
            this.Label1.Location = new System.Drawing.Point(0, 16);
            this.Label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(83, 20);
            this.Label1.TabIndex = 290;
            this.Label1.Text = "Comprobante";
            //
            // CmdBuscar
            //
            this.CmdBuscar.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.CmdBuscar.Location = new System.Drawing.Point(487, 7);
            this.CmdBuscar.Margin = new System.Windows.Forms.Padding(4);
            this.CmdBuscar.Name = "CmdBuscar";
            this.CmdBuscar.Size = new System.Drawing.Size(75, 30);
            this.CmdBuscar.TabIndex = 4;
            this.CmdBuscar.Text = "Buscar";
            this.CmdBuscar.Click += new System.EventHandler(this.CmdBuscar_Click);
            //
            // Label4
            //
            this.Label4.Location = new System.Drawing.Point(210, 14);
            this.Label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label4.Name = "Label4";
            this.Label4.Size = new System.Drawing.Size(53, 20);
            this.Label4.TabIndex = 293;
            this.Label4.Text = "Fecha";
            //
            // DtpFecini
            //
            this.DtpFecini.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFecini.Location = new System.Drawing.Point(249, 12);
            this.DtpFecini.Margin = new System.Windows.Forms.Padding(4);
            this.DtpFecini.Name = "DtpFecini";
            this.DtpFecini.Size = new System.Drawing.Size(99, 20);
            this.DtpFecini.TabIndex = 2;
            this.DtpFecini.ValueChanged += new System.EventHandler(this.DtpFecini_ValueChanged);
            //
            // Label2
            //
            this.Label2.Location = new System.Drawing.Point(356, 13);
            this.Label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(40, 18);
            this.Label2.TabIndex = 295;
            this.Label2.Text = "Inicial";
            //
            // DtpFecFin
            //
            this.DtpFecFin.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.DtpFecFin.Location = new System.Drawing.Point(393, 12);
            this.DtpFecFin.Margin = new System.Windows.Forms.Padding(4);
            this.DtpFecFin.Name = "DtpFecFin";
            this.DtpFecFin.Size = new System.Drawing.Size(86, 20);
            this.DtpFecFin.TabIndex = 3;
            this.DtpFecFin.ValueChanged += new System.EventHandler(this.DateTimePicker1_ValueChanged);
            //
            // FrmAyuDocs
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(570, 302);
            this.Controls.Add(this.DtpFecini);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.DtpFecFin);
            this.Controls.Add(this.Label4);
            this.Controls.Add(this.txtCpte);
            this.Controls.Add(this.CmdBuscar);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.txtConse);
            this.Controls.Add(this.DtgDocs);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmAyuDocs";
            this.ShowIcon = false;
            this.Text = "Comprobantes ";
            this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.FrmAyuDocs_KeyPress);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FrmAyuDocs_KeyUp);
            this.Load += new System.EventHandler(this.FrmAyuDocs_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DtgDocs)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        public System.Windows.Forms.DataGridView DtgDocs;
        public System.Windows.Forms.TextBox txtCpte;
        public System.Windows.Forms.TextBox txtConse;
        public System.Windows.Forms.Label Label1;
        public System.Windows.Forms.Button CmdBuscar;
        public System.Windows.Forms.Label Label4;
        public System.Windows.Forms.DateTimePicker DtpFecini;
        public System.Windows.Forms.Label Label2;
        public System.Windows.Forms.DateTimePicker DtpFecFin;
    }
}
