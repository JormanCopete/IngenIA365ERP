using System;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.Inventario.Forms
{
    public partial class frmclientes : Form
    {
        private ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
        private ERP.Core.Compartido.Utilidades.Ayuda msgsas = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.Contabilidad.Models.ParamCnt msgsyscnt = new ERP.Core.Contabilidad.Models.ParamCnt();

        public frmclientes(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void CmbGraba_Click(object sender, EventArgs e)
        {
            GrabaClientes();
            this.Close();
        }

        private void GrabaClientes()
        {
            msgcnt.GrabaTercero(this.TxtCliente.Text, this.TxtNombre.Text, this.txtDireccion.Text, this.TxtTelefono.Text, this.txtEmail.Text, "N", "0", this.mycon);
        }

        private void HelpTransaciones_Click(object sender, EventArgs e)
        {
            this.TxtCliente.Text = msgsyscnt.HelpNits(this.mycon, this);
            this.TxtCliente.Focus();
        }

        private void frmclientes_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    this.Close();
                    break;
                case Keys.F5:
                    GrabaClientes();
                    this.Close();
                    break;
            }
        }

        private void frmclientes_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
        }

        private void TxtCliente_LostFocus(object sender, EventArgs e)
        {
            if (this.TxtCliente.Text != "")
            {
                InitializaCampos();
                string _p1 = this.TxtNombre.Text;
                string _p2 = this.txtDireccion.Text;
                string _p3 = this.TxtTelefono.Text;
                string _p4 = this.txtEmail.Text;
                // msgcnt.BuscarTercero(this.TxtCliente.Text, this.mycon, ref _p1, ref _p2, ref _p3, ref _p4); // ERROR: CS7036
                this.TxtNombre.Text = _p1;
                this.txtDireccion.Text = _p2;
                this.TxtTelefono.Text = _p3;
                this.txtEmail.Text = _p4;
            }
        }

        private void InitializaCampos()
        {
            this.TxtNombre.Text = " ";
            this.txtDireccion.Text = " ";
            this.TxtTelefono.Text = " ";
            this.txtEmail.Text = " ";
        }

        private void TxtCliente_TextChanged(object sender, EventArgs e)
        {
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
