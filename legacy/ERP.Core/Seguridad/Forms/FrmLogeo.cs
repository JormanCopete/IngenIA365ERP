using ERP.Core.Compartido.Configuracion;
using System;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.Seguridad.Forms
{
    public partial class FrmLogeo : Form
    {
        private OdbcConnection mycon = new OdbcConnection();
        private ParamSys paramsys = new ParamSys();
        private string pass = " ";
        private bool sobregiro = false;
        private bool ok = false;
        private bool ValidaRetiros = false;
        private bool CancCdats = false;
        private bool cancelaPAP = false;
        private bool CancelaFacVenc = false;

        public enum TipoDeValidacion
        {
            Sobregiro = 0,
            TransRetirados = 1,
            CancelaCdtas = 2,
            cancelaPAP = 3,
            CancelaFacVenc = 4
        }

        public TipoDeValidacion TipoValidacion = TipoDeValidacion.Sobregiro;

        public FrmLogeo(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void FrmLogeo_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
        }

        private void TxtUsuario_Leave(object sender, EventArgs e)
        {
            if (TxtUsuario.Text.Trim() != "")
            {
                string login = TxtUsuario.Text;
                string nombre = "";
                object creditoMin = 0, creditoMax = 0, creditoGraMin = 0, creditoGraMax = 0;
                string grupo = "";
                bool cambiaPass = false;
                DateTime fechaCrea = DateTime.MinValue, fechaVence = DateTime.MinValue;
                bool validaCompro = false;
                string cedula = "";

                ok = paramsys.BuscaUsuario(ref login, mycon, ParamSys.Navega.Ninguno,
                    ref nombre, ref creditoMin, ref creditoMax, ref pass, ref grupo,
                    ref cambiaPass, ref fechaCrea, ref fechaVence, ref validaCompro,
                    ref sobregiro, ref creditoGraMin, ref creditoGraMax, ref cedula, ref ValidaRetiros);

                if (ok)
                {
                    switch (TipoValidacion)
                    {
                        case TipoDeValidacion.Sobregiro:
                            if (!sobregiro)
                            {
                                MessageBox.Show("Usuario no tiene permiso para realizar sobre giro.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                TxtUsuario.Text = "";
                                TxtUsuario.Focus();
                            }
                            break;
                        case TipoDeValidacion.TransRetirados:
                            if (!ValidaRetiros)
                            {
                                MessageBox.Show("Usuario no tiene permiso para realizar transacciones a retirados.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                TxtUsuario.Text = "";
                                TxtUsuario.Focus();
                            }
                            break;
                        case TipoDeValidacion.CancelaCdtas:
                            if (!CancCdats)
                            {
                                MessageBox.Show("Usuario no tiene permiso para realizar cancelacion de CDATs antes de la fecha de vencimiento del titulo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                TxtUsuario.Text = "";
                                TxtUsuario.Focus();
                            }
                            break;
                        case TipoDeValidacion.cancelaPAP:
                            if (!cancelaPAP)
                            {
                                MessageBox.Show("Usuario no tiene permiso para realizar cancelacion de PAP antes de la fecha de vencimiento del titulo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                TxtUsuario.Text = "";
                                TxtUsuario.Focus();
                            }
                            break;
                        case TipoDeValidacion.CancelaFacVenc:
                            if (!CancelaFacVenc)
                            {
                                MessageBox.Show("Usuario no tiene permiso para realizar pagos de facturas vencidas", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                TxtUsuario.Text = "";
                                TxtUsuario.Focus();
                            }
                            break;
                    }
                }
            }
        }

        private void TxtPassword_Leave(object sender, EventArgs e)
        {
            if (TxtUsuario.Text.Trim() != "" && TxtPassword.Text.Trim() != "")
            {
                string password = paramsys.EncripDescripPass(TxtPassword.Text, ParamSys.EncripDescrip.Encriptar);
                if (pass != password)
                {
                    MessageBox.Show("Contrasena errada.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    TxtPassword.Text = "";
                    TxtPassword.Focus();
                    BtnAceptar.Enabled = false;
                }
                else
                {
                    BtnAceptar.Enabled = true;
                }
            }
        }

        private void BtnCancelar_Click(object sender, EventArgs e)
        {
            TxtPassword.Text = "";
            TxtUsuario.Text = "";
            this.Close();
        }

        private void BtnAceptar_Click(object sender, EventArgs e)
        {
            this.Tag = TxtUsuario.Text;
            this.Close();
        }
    }
}
