using ERP.Core.CarteraFinanciera.Services.Depositos;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class dep_fsello : Form
    {
        private string curfilename = "";
        private ClsDepositos depositos = new ClsDepositos();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera car = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CarteraFinanciera.Models.ParamCop parCar = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys ParSys = new ERP.Core.Compartido.Configuracion.ParamSys();
        public string pstUsuario;
        private ERP.Core.Compartido.Utilidades.Ayuda msgayuda;
        private System.Data.Odbc.OdbcConnection conect = new System.Data.Odbc.OdbcConnection();
        public bool Grabacion = false;

        public dep_fsello(System.Data.Odbc.OdbcConnection conexion)
        {
            InitializeComponent();
            this.conect = conexion;
            msgayuda = new ERP.Core.Compartido.Utilidades.Ayuda(pstUsuario);
        }

        private void dep_fsello_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            if (Grabacion)
            {
                cmbsig.Text = "Capturar";
                Opciones.Visible = true;
            }
            else
            {
                cmbsig.Text = "Aceptar";
                Opciones.Visible = false;
            }
            PicSello.Image = depositos.CargarImagenSello(Convert.ToInt64(TxtCuenta.Text), conect);
        }

        private void cmbsig_Click(object sender, EventArgs e)
        {
            switch (cmbsig.Text)
            {
                case "Capturar":
                    curfilename = depositos.BuscaArchivo("JPG(*.JPG,JPEG)|*.JPG", "Archivo JPJ");
                    if (curfilename != "")
                        PicSello.Image = new Bitmap(curfilename);
                    break;
                case "Aceptar":
                    this.Close();
                    break;
            }
        }

        private void Opciones_ClickEvent(object sender, EventArgs e)
        {
            switch (Opciones.ButtonPressed)
            {
                case 2:
                    this.Close();
                    break;
                case 3:
                    depositos.GrabarImagenSello(Convert.ToInt64(TxtCuenta.Text), curfilename, conect);
                    PicSello.Image = depositos.CargarImagenSello(Convert.ToInt64(TxtCuenta.Text), conect);
                    break;
                case 4:
                    if (MessageBox.Show("Desea Eliminar la Firma?.", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        if (depositos.EliminarImagenSello(Convert.ToInt64(TxtCuenta.Text), conect))
                        {
                            MessageBox.Show("Firma Eliminada.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            PicSello.Image = null;
                        }
                    }
                    break;
            }
        }
    }
}
