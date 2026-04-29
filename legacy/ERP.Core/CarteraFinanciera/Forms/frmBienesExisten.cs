using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmBienesExisten : Form
    {
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        public DataSet DsVehiculosExisten = new DataSet();
        public DataSet DsBienesExisten = new DataSet();
        public DataSet DsBienesSeleccion = new DataSet();
        public DataSet DsBienesIncluidos = new DataSet();
        private bool ok;
        private string mysql;
        private int cuenta, i;
        public int tipo;
        public string codigoter;
        public string BuscarTabla = "";
        private OdbcConnection myconnect;

        public frmBienesExisten(OdbcConnection conexion)
        {
            InitializeComponent();
            this.myconnect = conexion;
        }

        private void frmBienesExisten_Load(object sender, EventArgs e)
        {
            string TipoBien1 = "Casa";
            string TipoBien2 = "Apartamento";
            string ststring = "";
            int stinteger = 0;
            double stdouble = 0;

            this.DgwBienesRaicesExisten.AutoGenerateColumns = false;
            if (!DsBienesExisten.Tables.Contains("TblBienExisten"))
            {
                DsBienesExisten.Tables.Add("TblBienExisten");
                DataColumnCollection cols = DsBienesExisten.Tables["TblBienExisten"].Columns;
                cols.Add("CLASE", ststring.GetType());
                cols.Add("NOMCLASE", ststring.GetType());
                cols.Add(tipo == 1 ? "DIRECCION" : "MARCA", ststring.GetType());
                cols.Add(tipo == 1 ? "CIUDAD" : "MODELO", tipo == 1 ? stinteger.GetType() : ststring.GetType());
                cols.Add("VALOR", stdouble.GetType());
                cols.Add("NOMCIUDAD", ststring.GetType());
            }
            else
            {
                this.DgwBienesRaicesExisten.DataSource = DsBienesExisten.Tables["TblBienExisten"];
            }

            if (BuscarTabla.Trim() == "cop_solcre")
            {
                mysql = "SELECT bi.clase, bi.direccion,bi.ciudad,bi.valor,bi.marca,bi.modelo, ci.nombre_ciudad " +
                        "FROM cop_solbienes bi left join cop_solcre so on bi.numsolicitud=so.numero left join sys_ciudad57 ci on bi.ciudad=ci.ciudad " +
                        "where  bi.bien='" + (tipo == 1 ? "R" : "V") + "' and so.codigoter='" + codigoter + "'" +
                        " group by bi.clase,bi.direccion,bi.ciudad,bi.valor,bi.marca,bi.modelo,ci.nombre_ciudad ";
            }
            else
            {
                mysql = "SELECT bi.clase, bi.direccion,bi.ciudad,bi.valor,bi.marca,bi.modelo, ci.nombre_ciudad " +
                        "FROM cop_maenitbienes bi left join sys_ciudad57 ci on bi.ciudad=ci.ciudad " +
                        "where  bi.bien='" + (tipo == 1 ? "R" : "V") + "' and bi.codigoter='" + codigoter + "'";
            }

            if (tipo == 2)
            {
                DgwBienesRaicesExisten.Columns[1].HeaderText = "Marca";
                DgwBienesRaicesExisten.Columns[2].HeaderText = "Modelo";
                TipoBien1 = "Particular";
                TipoBien2 = "P\u00fablico";
            }

            DataSet dsQuery = new DataSet();
            //ok = paramcop.ExecuteQueryDataset(mysql, myconnect, "frmExistenBienes", ref dsQuery, "TblBienExisten");
            if (ok)
            {
                // merge query results into DsBienesExisten
                cuenta = dsQuery.Tables["TblBienExisten"].Rows.Count;
                for (i = 0; i <= cuenta - 1; i++)
                {
                    DataRow r = dsQuery.Tables["TblBienExisten"].Rows[i];
                    DgwBienesRaicesExisten.Rows.Add(
                        Convert.ToInt32(r["clase"]) == 0 ? TipoBien1 : TipoBien2,
                        tipo == 1 ? r["direccion"] : r["marca"],
                        tipo == 1 ? r["nombre_ciudad"] : r["modelo"],
                        r["valor"],
                        r["ciudad"]);
                }
            }

            i = 0;
            if (tipo == 1)
            {
                if (DsBienesIncluidos.Tables.Contains("tblBienesRaices"))
                {
                    DataTable tbl = DsBienesIncluidos.Tables["tblBienesRaices"];
                    while (stinteger < tbl.Rows.Count)
                    {
                        while (i < DgwBienesRaicesExisten.RowCount)
                        {
                            if (tbl.Rows[stinteger]["NOMCLASE"].ToString() == DgwBienesRaicesExisten["clase", i].Value.ToString()
                                && tbl.Rows[stinteger]["direccion"].ToString() == DgwBienesRaicesExisten["direccion", i].Value.ToString()
                                && tbl.Rows[stinteger]["nomciudad"].ToString() == DgwBienesRaicesExisten["ciudad", i].Value.ToString()
                                && tbl.Rows[stinteger]["valor"].ToString() == DgwBienesRaicesExisten["valor", i].Value.ToString())
                            {
                                DgwBienesRaicesExisten.Rows[i].DefaultCellStyle.BackColor = System.Drawing.Color.Cyan;
                                DgwBienesRaicesExisten["Marcado", i].Value = "Y";
                                break;
                            }
                            i += 1;
                        }
                        i = 0;
                        stinteger += 1;
                    }
                }
            }
            else
            {
                if (DsBienesIncluidos.Tables.Contains("tblVehiculo"))
                {
                    DataTable tbl = DsBienesIncluidos.Tables["tblVehiculo"];
                    while (stinteger < tbl.Rows.Count)
                    {
                        while (i < DgwBienesRaicesExisten.RowCount)
                        {
                            if (tbl.Rows[stinteger]["NOMCLASE"].ToString() == DgwBienesRaicesExisten["clase", i].Value.ToString()
                                && tbl.Rows[stinteger]["marca"].ToString() == DgwBienesRaicesExisten["direccion", i].Value.ToString()
                                && tbl.Rows[stinteger]["modelo"].ToString() == DgwBienesRaicesExisten["ciudad", i].Value.ToString()
                                && tbl.Rows[stinteger]["valor"].ToString() == DgwBienesRaicesExisten["valor", i].Value.ToString())
                            {
                                DgwBienesRaicesExisten.Rows[i].DefaultCellStyle.BackColor = System.Drawing.Color.Cyan;
                                DgwBienesRaicesExisten["Marcado", i].Value = "Y";
                                break;
                            }
                            i += 1;
                        }
                        i = 0;
                        stinteger += 1;
                    }
                }
            }
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void MenAgregabien_Click(object sender, EventArgs e)
        {
            int fila = this.DgwBienesRaicesExisten.CurrentRow.Index;

            string ststring = "";
            int stinteger = 0;
            double stdouble = 0;

            if (!DsBienesSeleccion.Tables.Contains("TblBienSeleccion"))
            {
                DsBienesSeleccion.Tables.Add("TblBienSeleccion");
                DataColumnCollection cols = DsBienesSeleccion.Tables["TblBienSeleccion"].Columns;
                cols.Add("CLASE", ststring.GetType());
                cols.Add("NOMCLASE", ststring.GetType());
                cols.Add(tipo == 1 ? "DIRECCION" : "MARCA", ststring.GetType());
                cols.Add(tipo == 1 ? "CIUDAD" : "MODELO", tipo == 1 ? stinteger.GetType() : ststring.GetType());
                cols.Add("VALOR", stdouble.GetType());
                cols.Add("NOMCIUDAD", ststring.GetType());
            }

            this.DgwBienesRaicesExisten["Marcado", fila].Value = "Y";
            this.DgwBienesRaicesExisten.Rows[fila].DefaultCellStyle.BackColor = System.Drawing.Color.Cyan;

            DataGridViewRow dgRow = DgwBienesRaicesExisten.Rows[fila];
            if (tipo == 1)
            {
                DsBienesSeleccion.Tables["TblBienSeleccion"].Rows.Add(
                    dgRow.Cells["Clase"].Value.ToString() == "Casa" ? 0 : 1,
                    dgRow.Cells["Clase"].Value,
                    dgRow.Cells["direccion"].Value,
                    dgRow.Cells["codciudad"].Value,
                    dgRow.Cells["valor"].Value,
                    dgRow.Cells["ciudad"].Value);
            }
            else
            {
                DsBienesSeleccion.Tables["TblBienSeleccion"].Rows.Add(
                    dgRow.Cells["Clase"].Value.ToString() == "Particular" ? 0 : 1,
                    dgRow.Cells["Clase"].Value,
                    dgRow.Cells["direccion"].Value,
                    tipo == 1 ? dgRow.Cells["codciudad"].Value : dgRow.Cells["ciudad"].Value,
                    dgRow.Cells["valor"].Value,
                    dgRow.Cells["ciudad"].Value);
            }
        }

        private void MenQuitarbien_Click(object sender, EventArgs e)
        {
            // VB original is commented out - preserving empty implementation
        }

        private void Menu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            int fila = this.DgwBienesRaicesExisten.CurrentRow.Index;
            if (this.DgwBienesRaicesExisten["Marcado", fila].Value.ToString() == "Y")
            {
                this.MenAgregabien.Enabled = false;
                this.MenQuitarbien.Enabled = false;
            }
            else
            {
                this.MenAgregabien.Enabled = true;
                this.MenQuitarbien.Enabled = false;
            }
        }
    }
}
