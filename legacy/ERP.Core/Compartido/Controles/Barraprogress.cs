using ERP.Core.Compartido.Forms;
using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.ComponentModel;

namespace ERP.Core.Compartido.Controles
{
    public class Barraprogress : FrmProgres
    {
        private string MySql;
        private OdbcConnection MyConenct;
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();

        [DllImport("Kernel32.dll")]
        public static extern void Sleep(long dwMilliseconds);

        public Barraprogress(string titulo, Form Pertenese,
            ProgressBarStyle Estilo = ProgressBarStyle.Continuous,
            string Comentario = "Espere por favor.")
        {
            InitializeComponent();
            this.lblTittulo.Visible = true;
            this.ControlBox = false;
            this.Progress.Visible = true;
            this.lblTittulo.Text = titulo;
            this.Progress.Style = Estilo;
            this.Progress.Minimum = 0;
            this.Progress.Step = 1;
            this.Owner = Pertenese;
            this.ShowInTaskbar = false;
            this.mensaje.Text = Comentario;
        }

        public Barraprogress(string StSql, string Titulo, OdbcConnection myconect,
            Form Pertenese,
            ProgressBarStyle Estilo = ProgressBarStyle.Continuous,
            string Comentario = "Espere por favor.")
        {
            InitializeComponent();
            this.Sql = StSql;
            this.Coneccion = myconect;
            this.lblTittulo.Visible = true;
            this.ControlBox = false;
            this.Progress.Visible = true;
            this.lblTittulo.Text = Titulo;
            this.Progress.Style = Estilo;
            this.Progress.Minimum = 0;
            this.Progress.Step = 1;

            StSql = Microsoft.VisualBasic.Strings.Replace(StSql, "''", "' '", 1, -1, Microsoft.VisualBasic.CompareMethod.Text);

            DataSet ds = new DataSet();
            this.connect.ExecuteQueryDataset(StSql, myconect, "Barraprogress", ref ds, "new");

            this.Progress.Maximum = ds.Tables["new"].Rows.Count;
            this.Owner = Pertenese;
            this.ShowInTaskbar = false;
            this.mensaje.Text = Comentario;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public OdbcConnection Coneccion
        {
            get { return MyConenct; }
            set { MyConenct = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Sql
        {
            get { return MySql; }
            set { MySql = value; }
        }

        public void DefineMaximo(string StSql, OdbcConnection MyConeccion,
            string Titulo = "", string Comentario = "")
        {
            try
            {
                if (StSql.Trim() == "")
                {
                    throw new Exception("Falta la Instruccion Sql.");
                }
                if (Microsoft.VisualBasic.Strings.InStr(StSql, "Select", Microsoft.VisualBasic.CompareMethod.Text) < 0)
                {
                    throw new Exception("La instruccion no parece ser de consulta.");
                }
                if (MyConeccion == null)
                {
                    throw new Exception("No se a heredado la conexión.");
                }

                this.Sql = StSql;
                this.Coneccion = MyConeccion;

                string sqlTemp = Microsoft.VisualBasic.Strings.Replace(this.Sql, "''", "' '", 1, -1, Microsoft.VisualBasic.CompareMethod.Text);

                DataSet ds = new DataSet();
                this.connect.ExecuteQueryDataset(sqlTemp, MyConeccion, "Barraprogress", ref ds, "new");

                this.Progress.Minimum = 0;
                this.Progress.Value = 0;
                this.Progress.Maximum = ds.Tables["new"].Rows.Count;

                if (Titulo != "")
                {
                    this.lblTittulo.Text = Titulo;
                }
                if (Comentario != "")
                {
                    this.mensaje.Text = Comentario;
                }
            }
            finally
            {
            }
        }

        public new void PerformStep()
        {
            this.Tiempo.Enabled = true;
            Application.DoEvents();
            this.Progress.PerformStep();
        }

        private void Barraprogress_Load(object sender, EventArgs e)
        {
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // Barraprogress
            //
            this.AutoScaleDimensions = new SizeF(6.0F, 13.0F);
            this.ClientSize = new Size(355, 95);
            this.Name = "Barraprogress";
            this.Load += new EventHandler(this.Barraprogress_Load);
            this.ResumeLayout(false);
        }

        public void ValorMinimoMaximo(int Minimo = 0, int Maximo = 0, string Titulo = "")
        {
            try
            {
                if (Maximo < Minimo)
                {
                    Maximo = Minimo + 1;
                }
                this.Progress.Minimum = Minimo;
                this.Progress.Value = 0;
                this.Progress.Maximum = Maximo;
                if (Titulo != "")
                {
                    this.lblTittulo.Text = Titulo;
                }
            }
            finally
            {
            }
        }

        public void Titulo(string titulo = "Cargando.")
        {
            try
            {
                if (titulo != "")
                {
                    this.lblTittulo.Text = titulo;
                }
            }
            finally
            {
            }
        }

        public void Comentario(string comentario = "Espere por favor.")
        {
            try
            {
                if (comentario != "")
                {
                    this.mensaje.Text = comentario;
                }
            }
            finally
            {
            }
        }
    }
}
