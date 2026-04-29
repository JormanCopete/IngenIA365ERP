using ERP.Core.Contabilidad.Services;
// Traducción de: frmTipodoc.vb (msgcnt)
using Microsoft.VisualBasic;

namespace ERP.Core.Contabilidad.Forms
{
    public partial class frmTipodoc : System.Windows.Forms.Form
    {
        public bool Correcto = false;
        public string TipoDc;
        public double NumeroDc;

        public frmTipodoc()
        {
            InitializeComponent();
        }

        private void BtnAceptar_Click(object sender, System.EventArgs e)
        {
            if (ValidarDatos() == true)
            {
                Correcto = true;
                TipoDc = Microsoft.VisualBasic.Strings.Mid(this.CbxTipodocumento.Text, 1, 2);
                NumeroDc = System.Convert.ToDouble(this.TxtNumeroDocumento.Text);
                this.Close();
            }
            else
            {
                Correcto = false;
            }
        }

        bool ValidarDatos()
        {
            if (Information.IsNumeric(this.TxtNumeroDocumento.Text) == false)
            {
                System.Windows.Forms.MessageBox.Show("Debe digitar un número válido",
                    "SOLIDO", System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                this.TxtNumeroDocumento.Focus();
                return false;
            }
            return true;
        }

        private void frmTipodoc_Load(object sender, System.EventArgs e)
        {
            this.CbxTipodocumento.SelectedIndex = 0;
            this.CenterToScreen();
        }

        private void BtnCancelar_Click(object sender, System.EventArgs e)
        {
            Correcto = false;
            this.Close();
        }
    }
}
