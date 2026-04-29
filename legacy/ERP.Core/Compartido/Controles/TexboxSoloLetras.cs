using System;
using System.Windows.Forms;
using System.ComponentModel;

namespace ERP.Core.Compartido.Controles
{
    public class TexboxSoloLetras : System.Windows.Forms.TextBox
    {
        public bool _SoloLetras = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool SoloLetras
        {
            get { return _SoloLetras; }
            set { _SoloLetras = value; }
        }

        public TexboxSoloLetras()
        {
            this.KeyPress += new KeyPressEventHandler(TextBoxSoloNumeros_KeyPress);
        }

        private void TextBoxSoloNumeros_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (SoloLetras == true)
            {
                if (Char.IsLetter(e.KeyChar))
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
                    e.Handled = false;
                }
            }
        }
    }
}
