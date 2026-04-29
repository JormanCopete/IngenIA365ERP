using System;
using ERP.Core.Inventario.Services;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.Inventario.Forms
{
    public partial class frmLimiteVentas : Form
    {
        private ERP.Core.Compartido.Configuracion.ParamSys MsgSys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private msginv msginv = new msginv();
        private OdbcConnection myconnect = new OdbcConnection();

        public frmLimiteVentas(OdbcConnection conexion)
            : base()
        {
            InitializeComponent();
            this.myconnect = conexion;
        }

        public void AbrirConexion()
        {
            if (this.myconnect.State != ConnectionState.Open)
            {
                this.myconnect.Open();
            }
        }

        private void frmLimiteVentas_Load(object sender, EventArgs e)
        {
            this.CenterToParent();
            // MsgSys.ConfiguraForma(this, this.empresappl.Text); // ERROR: CS1501
            this.DgvCantidades.AutoGenerateColumns = true;
            this.chkAsociado.Focus();
        }

        private void cargaDatosInciales()
        {
            DataSet dataseDatosIniclas = new DataSet();
            // dataseDatosIniclas = msginv.buscaLimiteVentas(this.myconnect, this.Lblperiodo.Text, "GruposAsociado", this.lblInfoCodigoter.Text); // ERROR: CS1503
            this.DgvCantidades.DataSource = dataseDatosIniclas.Tables["tblimite"];
        }

        private void CargarDatos(string tipolimite)
        {
            DataSet dataseDatosIniclas = new DataSet();
            try
            {
                if (dataseDatosIniclas.Tables.Contains("tblimite"))
                {
                    dataseDatosIniclas.Tables.Clear();
                    DgvCantidades.Refresh();
                }
            }
            catch (Exception)
            {
            }

            switch (tipolimite)
            {
                case "Grupos":
                    if (this.chkAsociado.Checked)
                    {
                        // dataseDatosIniclas = msginv.buscaLimiteVentas(this.myconnect, this.Lblperiodo.Text, "GruposAsociado", this.lblInfoCodigoter.Text); // ERROR: CS1503
                        this.DgvCantidades.DataSource = dataseDatosIniclas.Tables["tblimite"];
                    }
                    else
                    {
                        // dataseDatosIniclas = msginv.buscaLimiteVentas(this.myconnect, this.Lblperiodo.Text, "Grupos"); // ERROR: CS7036
                        this.DgvCantidades.DataSource = dataseDatosIniclas.Tables["tblimite"];
                    }
                    break;

                case "Productos":
                    if (this.chkAsociado.Checked)
                    {
                        // dataseDatosIniclas = msginv.buscaLimiteVentas(this.myconnect, this.Lblperiodo.Text, "ProductosAsociado", this.lblInfoCodigoter.Text); // ERROR: CS1503
                        this.DgvCantidades.DataSource = dataseDatosIniclas.Tables["tblimite"];
                    }
                    else
                    {
                        // dataseDatosIniclas = msginv.buscaLimiteVentas(this.myconnect, this.Lblperiodo.Text, "Productos"); // ERROR: CS7036
                        this.DgvCantidades.DataSource = dataseDatosIniclas.Tables["tblimite"];
                    }
                    break;
            }
        }

        private void rdbGrupos_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbGrupos.Checked)
            {
                CargarDatos("Grupos");
            }
        }

        private void rdbProductos_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbProductos.Checked)
            {
                CargarDatos("Productos");
            }
        }

        private void chkAsociado_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbProductos.Checked)
            {
                CargarDatos("Productos");
            }
            if (rdbGrupos.Checked)
            {
                CargarDatos("Grupos");
            }
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
