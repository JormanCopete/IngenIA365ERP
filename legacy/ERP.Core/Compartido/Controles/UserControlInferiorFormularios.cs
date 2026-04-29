using ERP.Core.Compartido.Utilidades;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Controles
{
    public partial class UserControlInferiorFormularios : UserControl
    {
        public Ayuda cargarAyuda = new Ayuda("admin");
        private Form fomulario;

        [Description("Texto del Nombre  De el Usuario"), Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string NombreUsuario
        {
            get { return this.txtusuario.Text; }
            set { this.txtusuario.Text = value; }
        }

        public UserControlInferiorFormularios()
        {
            InitializeComponent();
        }

        private void UserControlInferiorFormularios_Load(object sender, EventArgs e)
        {
        }

        public void obtenerDatosTxtInferiores(Form fomulario)
        {
            string _empresa = "";
            string _servidor = this.txtserver0.Text;
            string _bdatos = this.txtbdatos.Text;
            string _progname = this.txtprogname.Text;
            string _usuario = "";
            string _fechahoy = this.txtfechahoy.Text;

            cargarAyuda.ConfiguraForma(fomulario, ref _empresa, ref _servidor, ref _bdatos, ref _progname, ref _usuario, ref _fechahoy);

            this.txtserver0.Text = _servidor;
            this.txtbdatos.Text = _bdatos;
            this.txtprogname.Text = _progname;
            this.txtfechahoy.Text = _fechahoy;
        }
    }
}
