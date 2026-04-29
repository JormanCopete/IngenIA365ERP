using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class cop_fopciondescuentos : Form
    {
        public char OpcionSeleccionada = '0';

        public cop_fopciondescuentos()
        {
            InitializeComponent();
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            Seleccionado('0');
        }

        private void cop_fopcionesproyeccion_Load(object sender, EventArgs e)
        {
        }

        public void Seleccionado(char Boton)
        {
            if (Boton != '0')
            {
                if (Validar() == false)
                {
                    return;
                }
            }
            OpcionSeleccionada = Boton;
            this.Close();
        }

        public bool Validar()
        {
            if (this.TxtCiclo.Text.Trim() == "" || this.TxtCiclo.Text.Length != 6)
            {
                MessageBox.Show("Debe digitar ciclo hasta el cual se incluiran las cuotas atrasadas", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (Information.IsNumeric(this.TxtCiclo.Text) == false)
            {
                MessageBox.Show("El ciclo debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void BtnOpcion1_Click(object sender, EventArgs e)
        {
            Seleccionado('1');
        }

        private void BtnOpcion2_Click(object sender, EventArgs e)
        {
            Seleccionado('2');
        }
    }
}
