using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class FrmGestionCob : Form
    {
        private System.Data.Odbc.OdbcConnection mycon = new System.Data.Odbc.OdbcConnection();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private DataSet dsdata = new DataSet(), dsdatos = new DataSet(), dsdetalle = new DataSet();
        private DateTime fecfin;
        public bool ProcesoManual = false;

        public FrmGestionCob(System.Data.Odbc.OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void FrmGestionCob_Load(object sender, EventArgs e)
        {
            int diafin = 30;
            this.LblNombre.Text = " ";
            if (this.TxtCodigoter.Text.Trim() != "")
            {
                diafin = DateTime.DaysInMonth(Convert.ToInt32(this.LblPeriodo.Text.Substring(0, 4)), Convert.ToInt32(this.LblPeriodo.Text.Substring(4)));
                this.DtpFechaInicial.Value = new DateTime(Convert.ToInt32(this.LblPeriodo.Text.Substring(0, 4)), Convert.ToInt32(this.LblPeriodo.Text.Substring(4)), 1);
                this.DtpFechaFinal.Value = new DateTime(Convert.ToInt32(this.LblPeriodo.Text.Substring(0, 4)), Convert.ToInt32(this.LblPeriodo.Text.Substring(4)), diafin);
                //this.paramcop.BuscaAsociado(this.TxtCodigoter.Text, this.mycon, "", "", "", "", this.LblNombre.Text);
                fecfin = this.DtpFechaFinal.Value;
                CargaGrilla();
            }
        }

        private void CargaGrilla()
        {
            if (this.DgwGrilla.DataSource == null)
            {
                this.DgwGrilla.Rows.Clear();
            }
            else
            {
                this.DgwGrilla.DataSource = null;
            }
            dsdata = this.msgcop.CargarReporteGestiones("Todos", this.DtpFechaInicial.Value, this.DtpFechaFinal.Value, this.TxtCodigoter.Text, this.TxtCodigoter.Text, this, fecfin, this.mycon);
            if (dsdata.Tables["TblMaesGestion"].Rows.Count > 0)
            {
                this.DgwGrilla.AutoGenerateColumns = false;
                this.DgwGrilla.DataSource = dsdata.Tables["TblMaesGestion"];
            }
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BtnActualizar_Click(object sender, EventArgs e)
        {
            CargaGrilla();
        }

        private void confidataset(ref DataSet dataset)
        {
            int a = 0;
            string j = " ";
            double d = 0;
            dataset.Tables.Add("TblDetalle");
            dataset.Tables["TblDetalle"].Columns.Add("lincred", a.GetType());
            dataset.Tables["TblDetalle"].Columns.Add("numero", d.GetType());
            dataset.Tables["TblDetalle"].Columns.Add("deuda", d.GetType());
            dataset.Tables["TblDetalle"].Columns.Add("pago", d.GetType());
            dataset.Tables["TblDetalle"].Columns.Add("diferencia", d.GetType());
            dataset.Tables["TblDetalle"].Columns.Add("fecha", j.GetType());
            dataset.Tables["TblDetalle"].Columns.Add("estadoObli", j.GetType());
        }

        private void CargarDetallesNoComprometidas()
        {
            DataSet dataset = new DataSet();
            if (this.DgwGrilla.RowCount > 0)
            {
                dataset = this.msgcop.CargarDetallesCobranza(Convert.ToInt32(this.DgwGrilla.CurrentRow.Cells["ClmId"].Value), this.mycon);
                if (dataset.Tables["TblDetalleGestion"].Rows.Count > 0)
                {
                    this.TxtDetalle.Text = dataset.Tables["TblDetalleGestion"].Rows[0]["detalle"].ToString();
                    this.DtpFecCompromiso.Visible = false;
                    this.Label5.Visible = false;
                    CargaGrillaDetalle(dataset.Tables["TblDetalleGestion"]);
                    this.PnlGrillaDetalles.Visible = true;
                }
            }
        }

        private void DgwGrilla_DoubleClick(object sender, EventArgs e)
        {
            DataSet data = new DataSet();
            int n = 0;
            string detalle = " ";
            DateTime fechacompromiso = default(DateTime);
            try
            {
                if (this.DgwGrilla.RowCount > 0)
                {
                    this.DtpFecCompromiso.Visible = true;
                    this.Label5.Visible = true;
                    if (this.DgwGrilla.CurrentRow.Cells["clmest"].Value.ToString() != "3")
                    {
                        CargarDetallesNoComprometidas();
                    }
                    else
                    {
                        confidataset(ref data);
                        DataTable tbl = dsdata.Tables["TblConsulta"];
                        while (n < tbl.Rows.Count)
                        {
                            if (tbl.Rows[n]["id"].ToString() == this.DgwGrilla.CurrentRow.Cells["ClmId"].Value.ToString() &&
                                string.Format("{0:dd-mm-yyyy}", tbl.Rows[n]["FechaCompromiso"]) == string.Format("{0:dd-mm-yyyy}", this.DgwGrilla.CurrentRow.Cells["clmfeccompromiso"].Value))
                            {
                                data.Tables["TblDetalle"].Rows.Add(tbl.Rows[n]["lincred"], tbl.Rows[n]["numero"], tbl.Rows[n]["deuda"], tbl.Rows[n]["pago"], tbl.Rows[n]["diferencia"], tbl.Rows[n]["fecha"], tbl.Rows[n]["estadoObli"]);
                                detalle = tbl.Rows[n]["detalle"].ToString();
                                fechacompromiso = Convert.ToDateTime(tbl.Rows[n]["fechacompromiso"]);
                            }
                            n = n + 1;
                        }
                        if (data.Tables["TblDetalle"].Rows.Count > 0)
                        {
                            this.TxtDetalle.Text = detalle;
                            this.DtpFecCompromiso.Value = fechacompromiso;
                            CargaGrillaDetalle(data.Tables["TblDetalle"]);
                            this.PnlGrillaDetalles.Visible = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void CargaGrillaDetalle(DataTable data)
        {
            if (this.DgwGrillaDetalles.DataSource == null)
            {
                this.DgwGrillaDetalles.Rows.Clear();
            }
            else
            {
                this.DgwGrillaDetalles.DataSource = null;
            }
            this.DgwGrillaDetalles.AutoGenerateColumns = false;
            this.DgwGrillaDetalles.DataSource = data;
        }

        private void BtnSalirPanel_Click(object sender, EventArgs e)
        {
            this.TxtDetalle.Text = " ";
            this.DtpFecCompromiso.Value = DateTime.Now;
            this.PnlGrillaDetalles.Visible = false;
        }

        private void VolverAGestionarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.DgwGrilla.DataSource != null)
            {
                this.Tag = this.DgwGrilla.CurrentRow.Cells["ClmId"].Value;
            }
            this.Close();
        }

        private void DgwGrilla_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.ProcesoManual == true)
            {
                if (e.Button == MouseButtons.Right)
                {
                    if (this.DgwGrilla.RowCount > 0)
                    {
                        if (Information.IsNumeric(this.DgwGrilla.CurrentRow.Cells["ClmId"].Value) == true)
                        {
                            this.VolverAGestionarToolStripMenuItem.Enabled = true;
                        }
                        else
                        {
                            this.VolverAGestionarToolStripMenuItem.Enabled = false;
                        }
                    }
                    this.MenuProceso.Show(this.DgwGrilla, new System.Drawing.Point(e.X, e.Y));
                }
            }
        }

        private void DgwGrilla_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }
    }
}
