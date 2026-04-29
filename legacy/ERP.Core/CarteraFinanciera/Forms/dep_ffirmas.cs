using ERP.Core.CarteraFinanciera.Services.Depositos;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class dep_ffirmas : Form
    {
        private string curfilename = "";
        private ClsDepositos depositos = new ClsDepositos();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera car = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CarteraFinanciera.Models.ParamCop parCar = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys ParSys = new ERP.Core.Compartido.Configuracion.ParamSys();
        public string pstUsuario;
        private ERP.Core.Compartido.Utilidades.Ayuda msgayuda;
        private System.Data.Odbc.OdbcConnection conect = new System.Data.Odbc.OdbcConnection();
        public int NumFirmas;

        public dep_ffirmas(System.Data.Odbc.OdbcConnection conexion)
        {
            InitializeComponent();
            this.conect = conexion;
            msgayuda = new ERP.Core.Compartido.Utilidades.Ayuda(pstUsuario);
        }

        private void dep_ffirmas_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            bool _req = CkBfirmareque.Checked;
            PicFirma.Image = depositos.CargarImagen(Convert.ToInt64(TxtCuenta.Text), Convert.ToInt32(lblNumFirma.Text), conect, ref _req);
        }

        private void cmbsig_Click(object sender, EventArgs e)
        {
            if (cmbsig.Text == "Capturar")
            {
                curfilename = depositos.BuscaArchivo("JPG(*.JPG,JPEG)|*.JPG", "Archivo JPJ");
                if (curfilename != "")
                    PicFirma.Image = new Bitmap(curfilename);
            }
        }

        private void MacToolB1_ClickEvent(object sender, EventArgs e)
        {
            switch (MacToolB1.ButtonPressed)
            {
                case 2:
                    this.Close();
                    break;
                case 3:
                    depositos.GrabarImagenFirma(Convert.ToInt64(TxtCuenta.Text), Convert.ToInt32(lblNumFirma.Text), curfilename, conect, CkBfirmareque.Checked);
                    bool _req3 = CkBfirmareque.Checked;
                    PicFirma.Image = depositos.CargarImagen(Convert.ToInt64(TxtCuenta.Text), Convert.ToInt32(lblNumFirma.Text), conect, ref _req3);
                    break;
                case 4:
                    if (MessageBox.Show("Desea Eliminar la Firma?.", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        if (depositos.EliminarImagenFirma(Convert.ToInt64(TxtCuenta.Text), Convert.ToInt32(lblNumFirma.Text), conect))
                        {
                            MessageBox.Show("Firma Eliminada.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            PicFirma.Image = null;
                        }
                    }
                    break;
                case 5:
                    lblNumFirma.Text = "1";
                    break;
                case 6:
                    int n6 = Convert.ToInt32(lblNumFirma.Text) - 1;
                    lblNumFirma.Text = (n6 < 1 ? 1 : n6).ToString();
                    break;
                case 7:
                    int n7 = Convert.ToInt32(lblNumFirma.Text) + 1;
                    lblNumFirma.Text = (n7 > NumFirmas ? NumFirmas : n7).ToString();
                    break;
                case 8:
                    lblNumFirma.Text = NumFirmas.ToString();
                    break;
            }

            if (MacToolB1.ButtonPressed > 4)
            {
                bool _reqN = CkBfirmareque.Checked;
                PicFirma.Image = depositos.CargarImagen(Convert.ToInt64(TxtCuenta.Text), Convert.ToInt32(lblNumFirma.Text), conect, ref _reqN);
            }
        }

        private void TxtCuenta_TextChanged(object sender, EventArgs e)
        {
        }
    }
}
