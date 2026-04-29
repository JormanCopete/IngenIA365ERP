using ERP.Core.CarteraFinanciera.Services.Depositos;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class dep_fvenfirma : Form
    {
        private int numFirma = 1;
        public int Firmareq;
        private int numPagina = 0;
        private bool Requerida = false;
        private ERP.Core.Compartido.Datos.ClsConect dbconect = new ERP.Core.Compartido.Datos.ClsConect();
        private ClsDepositos depositos = new ClsDepositos();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera car = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CarteraFinanciera.Models.ParamCop parCar = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys ParSys = new ERP.Core.Compartido.Configuracion.ParamSys();
        public string pstUsuario;
        private ERP.Core.Compartido.Utilidades.Ayuda msgayuda;
        private System.Data.Odbc.OdbcConnection conect = new System.Data.Odbc.OdbcConnection();

        public dep_fvenfirma(System.Data.Odbc.OdbcConnection conexion)
        {
            InitializeComponent();
            this.conect = conexion;
            msgayuda = new ERP.Core.Compartido.Utilidades.Ayuda(pstUsuario);
        }

        private void dep_fvenfirma_Load(object sender, EventArgs e)
        {
            dbconect.MyOdbcConect(ref Var.varini);
            dbconect.LlenarVarini(ref Var.varini);
            server0.Text = Var.varini.pstServer;
            bdatos.Text = Var.varini.pstBdatos;
            usuario.Text = Var.varini.pstUsuario;
            empresappl.Text = Var.varini.pstEmpresa;
            progname.Text = this.Name;
            fechahoy.Text = DateTime.Now.ToString().Substring(0, 10);
            this.CenterToScreen();
            Lblperiodo.Text = "999999";
            string dummy1 = null, dummy2 = null, dummy3 = null, dummy4 = null;
            string periodo = Lblperiodo.Text;
           // ParSys.buscaPeriodo("ahor", conect, ref dummy1, ref dummy2, ref dummy3, ref dummy4, ref periodo);
            Lblperiodo.Text = periodo;
            CargarImagen(Convert.ToInt64(txtcuenta.Text));
        }

        private void CargarImagen(long NumCuenta)
        {
            try
            {
                numPagina = 0;
                for (numFirma = 1; numFirma <= 4; numFirma++)
                {
                    Bitmap bitmap1 = depositos.CargarImagen(Convert.ToInt64(txtcuenta.Text), numFirma, conect, ref Requerida);
                    switch (numFirma - numPagina)
                    {
                        case 1:
                            PicFirma1.Image = bitmap1;
                            Lblreq1.Visible = Requerida;
                            break;
                        case 2:
                            Picfirma2.Image = bitmap1;
                            Lblreq2.Visible = Requerida;
                            break;
                        case 3:
                            picfirma3.Image = bitmap1;
                            Lblreq3.Visible = Requerida;
                            break;
                        case 4:
                            Picfirma4.Image = bitmap1;
                            Lblreq4.Visible = Requerida;
                            break;
                    }
                }
                numPagina += 4;
            }
            catch
            {
                PicFirma1.Image = null;
                PicFirma1.Refresh();
            }
        }

        private void cmbsiguiente_Click(object sender, EventArgs e)
        {
            int i;
            for (i = numFirma; i <= Firmareq; i++)
            {
                Bitmap bitmap1 = depositos.CargarImagen(Convert.ToInt64(txtcuenta.Text), i, conect);
                switch (i - numPagina)
                {
                    case 1:
                        PicFirma1.Image = bitmap1;
                        Lblreq1.Visible = Requerida;
                        break;
                    case 2:
                        Picfirma2.Image = bitmap1;
                        Lblreq2.Visible = Requerida;
                        break;
                    case 3:
                        picfirma3.Image = bitmap1;
                        Lblreq3.Visible = Requerida;
                        break;
                    case 4:
                        Picfirma4.Image = bitmap1;
                        Lblreq4.Visible = Requerida;
                        break;
                }
            }
            numPagina += 4;
            numFirma = i;
        }

        private void MacToolB1_ClickEvent(object sender, EventArgs e)
        {
            switch (MacToolB1.ButtonPressed)
            {
                case 1:
                case 2:
                    this.Close();
                    break;
            }
        }

        private void CmbPrincipio_Click(object sender, EventArgs e)
        {
            CargarImagen(Convert.ToInt64(txtcuenta.Text));
        }
    }
}
