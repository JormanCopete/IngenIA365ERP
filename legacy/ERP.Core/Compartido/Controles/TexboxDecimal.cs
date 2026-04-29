using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Controles
{
    public partial class TexboxDecimal : System.Windows.Forms.TextBox
    {
        public bool _SoloDecimal = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool SoloDecimal
        {
            get { return _SoloDecimal; }
            set { _SoloDecimal = value; }
        }

        public TexboxDecimal()
        {
            InitializeComponent();
            this.KeyPress += new KeyPressEventHandler(TextBoxSoloNumeros_KeyPress);
        }

        private void TextBoxSoloNumeros_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (SoloDecimal == true)
            {
                if ("0123456789.\b".IndexOf(e.KeyChar) < 0)
                {
                    e.KeyChar = '\0';
                }
            }
        }
    }
}
