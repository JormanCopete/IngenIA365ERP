using ERP.Core.Compartido.Utilidades;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Forms
{
    public partial class FrmAyuDocs : Form
    {
        public double Consecutivo;
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.Compartido.Datos.ClsConect MyOdbcConet = new ERP.Core.Compartido.Datos.ClsConect();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        public Ayuda ayuda;

        public FrmAyuDocs(OdbcConnection conexion)
        {
            InitializeComponent();
            MyOdbcConet.MyOdbcConect(ref varini);
            ayuda = new Ayuda(varini.pstUsuario);
            this.mycon = conexion;
        }

        private void FrmAyuDocs_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void FrmAyuDocs_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Consecutivo = 0;
                this.Dispose();
                this.Close();
            }
        }

        private void FrmAyuDocs_Load(object sender, EventArgs e)
        {
            DataSet DtDatos = new DataSet();
            switch (Convert.ToString(this.Tag))
            {
                case "cop":
                    string comprobanteCop = this.txtCpte.Text;
                    DtDatos = ayuda.AyudaDocsCop(ref comprobanteCop, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "cont":
                    string comprobanteCont = this.txtCpte.Text;
                    DtDatos = ayuda.AyudaDocsCnt(ref comprobanteCont, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "post":
                    int idTipoMovtoPost = 0;
                    int.TryParse(this.txtCpte.Text, out idTipoMovtoPost);
                    DtDatos = ayuda.AyudaDocspost(ref idTipoMovtoPost, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "susp":
                    int idTipoMovtoSusp = 0;
                    int.TryParse(this.txtCpte.Text, out idTipoMovtoSusp);
                    DtDatos = ayuda.AyudaDocsSuspe(ref idTipoMovtoSusp, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "PostCoti":
                    int idTipoMovtoCoti = 0;
                    int.TryParse(this.txtCpte.Text, out idTipoMovtoCoti);
                    DtDatos = ayuda.AyudaDocspostCosti(ref idTipoMovtoCoti, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "PostOrdComp":
                    int idTipoMovtoOrdComp = 0;
                    int.TryParse(this.txtCpte.Text, out idTipoMovtoOrdComp);
                    DtDatos = ayuda.AyudaDocspostOrdComp(ref idTipoMovtoOrdComp, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
            }

            DtgDocs.DataSource = DtDatos.Tables[0];
            DtgDocs.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            DtgDocs.Columns[3].DefaultCellStyle.Format = "N2";
            string tagStr = Convert.ToString(this.Tag);
            if (tagStr != "post" && tagStr != "susp" && tagStr != "PostCoti" && tagStr != "PostOrdComp")
            {
                DtgDocs.Columns[2].DefaultCellStyle.Format = "N2";
                DtgDocs.Columns[5].Width = 20;
                DtgDocs.Columns[6].Width = 20;
            }
            else
            {
                DtgDocs.Columns[0].Width = 40;
                DtgDocs.Columns[5].Width = 30;
                DtgDocs.Columns[6].Width = 60;
                DtgDocs.Columns[7].Width = 250;
            }

            this.CenterToScreen();
        }

        private void DtgDocs_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void DtgDocs_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            Consecutivo = Convert.ToDouble(this.DtgDocs.SelectedCells[1].Value);
            this.Dispose();
            this.Close();
        }

        private void DateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
        }

        private void CmdBuscar_Click(object sender, EventArgs e)
        {
            DataSet DtDatos = new DataSet();

            string tagStr = Convert.ToString(this.Tag);
            switch (tagStr)
            {
                case "cop":
                    string comprobante = this.txtCpte.Text;
                    double consecutivo = 0;
                    double.TryParse(this.txtConse.Text.Trim(), out consecutivo);
                    DtDatos = ayuda.AyudaDocsCop(ref comprobante, consecutivo, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "cont":    
                    string comprobanteCont = this.txtCpte.Text;
                    double consecutivoCont = 0;
                    double.TryParse(this.txtConse.Text.Trim(), out consecutivoCont);
                    DtDatos = ayuda.AyudaDocsCnt(ref comprobanteCont, consecutivoCont, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "post":
                    int idTipoMovtoPost = 0;
                    int.TryParse(this.txtCpte.Text, out idTipoMovtoPost);
                    DtDatos = ayuda.AyudaDocspost(ref idTipoMovtoPost, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "susp":
                    int idTipoMovtoSusp = 0;
                    int.TryParse(this.txtCpte.Text, out idTipoMovtoSusp);
                    DtDatos = ayuda.AyudaDocsSuspe(ref idTipoMovtoSusp, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "PostCoti":
                    int idTipoMovtoCoti = 0;
                    int.TryParse(this.txtCpte.Text, out idTipoMovtoCoti);
                    DtDatos = ayuda.AyudaDocspostCosti(ref idTipoMovtoCoti, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
                case "PostOrdComp":
                    int idTipoMovtoOrdComp = 0;
                    int.TryParse(this.txtCpte.Text, out idTipoMovtoOrdComp);
                    DtDatos = ayuda.AyudaDocspostOrdComp(ref idTipoMovtoOrdComp, 0, this.DtpFecini.Value, this.DtpFecFin.Value, mycon);
                    break;
            }

            DtgDocs.DataSource = DtDatos.Tables[0];
            DtgDocs.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (tagStr != "post" && tagStr != "susp" && tagStr != "PostCoti" && tagStr != "PostOrdComp")
            {
                DtgDocs.Columns[5].Width = 20;
                DtgDocs.Columns[6].Width = 20;
            }
        }

        private void DtpFecini_ValueChanged(object sender, EventArgs e)
        {
        }

        private void txtConse_TextChanged(object sender, EventArgs e)
        {
        }
    }
}
