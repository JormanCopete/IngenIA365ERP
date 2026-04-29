using System;
using System.Windows.Forms;
using System.ComponentModel;

namespace ERP.Core.Compartido.Controles
{
    public class TexboxSoloNumeros : System.Windows.Forms.TextBox
    {
        public bool _SoloNumeros = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool SoloNumeros
        {
            get { return _SoloNumeros; }
            set { _SoloNumeros = value; }
        }

        public TexboxSoloNumeros()
        {
            this.KeyPress += new KeyPressEventHandler(TextBoxSoloNumeros_KeyPress);
        }

        private void TextBoxSoloNumeros_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (SoloNumeros == true)
            {
                if (Char.IsNumber(e.KeyChar))
                {
                    e.Handled = false;
                }
                else if (Char.IsControl(e.KeyChar))
                {
                    e.Handled = false;
                }
                else if (Char.IsSeparator(e.KeyChar))
                {
                    e.Handled = false;
                }
                else
                {
                    e.Handled = true;
                }
            }
        }
    }
}
