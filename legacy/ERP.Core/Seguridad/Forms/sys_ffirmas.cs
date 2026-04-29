using ERP.Core.CarteraFinanciera.Models;
using ERP.Core.Compartido.Configuracion;
using ERP.Core.Compartido.Utilidades;
using System;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;

namespace ERP.Core.Seguridad.Forms
{
    public partial class sys_ffirmas : Form
    {
        private string curfilename = "";
        private ParamCop parCar = new ParamCop();
        private ParamSys ParSys = new ParamSys();
        public string pstUsuario;
        private ERP.Core.Compartido.Utilidades.Ayuda msgayuda;
        private OdbcConnection conect = new OdbcConnection();
        public string opcion = "";
        public int Num_cargo;
        public string empresa;
        private string coluncargo;

        public sys_ffirmas(OdbcConnection conexion)
        {
            InitializeComponent();
            this.conect = conexion;
            msgayuda = new ERP.Core.Compartido.Utilidades.Ayuda(pstUsuario);
        }

        private void Btn_capturar_Click(object sender, EventArgs e)
        {
            if (Btn_capturar.Text == "Capturar")
            {
                curfilename = ParSys.BuscaArchivo("JPG(*.JPG,JPEG)|*.JPG", "Archivo JPJ");
                if (curfilename != "")
                {
                    PicFirma.Image = new Bitmap(curfilename);
                }
            }
        }

        private void sys_ffirmas_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            if (opcion == "")
            {
                string nombreFirma = Lbl_Nombre.Text;
                string cargoFirma = Lbl_cargo.Text;
                string firmaCol = coluncargo;
                PicFirma.Image = ParSys.CargarImagen(Num_cargo, conect, empresa, ref nombreFirma, ref cargoFirma, ref firmaCol);
                Lbl_Nombre.Text = nombreFirma;
                Lbl_cargo.Text = cargoFirma;
                coluncargo = firmaCol;
                texto_cargo();
            }
            else
            {
                PicFirma.Image = parCar.CargarImagen(conect, opcion);
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
                    if (opcion == "")
                    {
                        ParSys.GrabarImagenFirma(empresa, coluncargo, curfilename, conect);
                        string nombreFirma = Lbl_Nombre.Text;
                        string cargoFirma = Lbl_cargo.Text;
                        string firmaCol = coluncargo;
                        PicFirma.Image = ParSys.CargarImagen(Num_cargo, conect, empresa, ref nombreFirma, ref cargoFirma, ref firmaCol);
                        Lbl_Nombre.Text = nombreFirma;
                        Lbl_cargo.Text = cargoFirma;
                        coluncargo = firmaCol;
                        texto_cargo();
                    }
                    else
                    {
                        parCar.GrabarImagenFirma(opcion, curfilename, conect);
                        PicFirma.Image = parCar.CargarImagen(conect, opcion);
                    }
                    break;
                case 4:
                    if (MessageBox.Show("Desea Eliminar la Firma?.", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        if (opcion == "")
                        {
                            if (ParSys.EliminarImagenFirma(empresa, coluncargo, conect))
                            {
                                MessageBox.Show("Firma Eliminada.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                PicFirma.Image = null;
                            }
                        }
                        else
                        {
                            if (parCar.EliminarImagenFirma(opcion, conect))
                            {
                                MessageBox.Show("Firma Eliminada.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                PicFirma.Image = null;
                            }
                        }
                    }
                    break;
            }
        }

        private void MacToolB1_Load(object sender, EventArgs e)
        {
        }

        private void texto_cargo()
        {
            switch (Num_cargo)
            {
                case 1:
                    Lbl_cargo.Text = "REPRESENTANTE LEGAL";
                    break;
                case 2:
                    Lbl_cargo.Text = "CONTADOR";
                    break;
                case 3:
                    Lbl_cargo.Text = "REVISOR FISCAL";
                    break;
                case 4:
                    Lbl_cargo.Text = "JEFE CARTERA";
                    break;
            }
        }
    }
}
