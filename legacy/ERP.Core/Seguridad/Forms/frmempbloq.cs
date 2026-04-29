using System;
using System.Data;
using System.Windows.Forms;
using System.ComponentModel;

namespace ERP.Core.Seguridad.Forms
{
    public partial class frmempbloq : Form
    {
        private DataSet _dstempresabloq = new DataSet();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataSet dstempresabloq
        {
            get { return _dstempresabloq; }
            set { _dstempresabloq = value; }
        }

        public frmempbloq()
        {
            InitializeComponent();
        }

        private void frmempbloq_Load(object sender, EventArgs e)
        {
            if (dstempresabloq.Tables.Contains("tbl_empBloq"))
            {
                dtgEmpBloq.DataSource = dstempresabloq.Tables["tbl_empBloq"];
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
