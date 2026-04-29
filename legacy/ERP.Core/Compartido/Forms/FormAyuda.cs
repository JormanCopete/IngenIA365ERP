using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Forms
{
    public partial class FormAyuda : Form
    {
        private string stCampodosA, stCampodosB;
        private bool boEncontro, pboBusca1;
        public string stMysql2 = null;
        public string Str_codAso;
        public string stRespu, stRes1, stRes2;
        private OdbcConnection pmyConecayuda;
        private ERP.Core.Compartido.Datos.ClsConect MyOdbcConet = new ERP.Core.Compartido.Datos.ClsConect();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();

        private string sttop, stlimit, stRowNum;

        public FormAyuda(OdbcConnection myconect)
        {
            MyOdbcConet.MyOdbcConect(ref varini);
            InitializeComponent();
            pmyConecayuda = myconect;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            string mysql = "";
            stCampodosA = "";
            if (stNombretabla.Text == "sys_maenit")
            {
                stCampocuatro.Text = "codigo_empresa";
            }

            if (valor1.Text == "" && valor2.Text == "" && valor3.Text == "" && txtValor4.Text == "" && stCampocuatro.Text.Trim() != "")
            {
                if (stCampotres.Text.Trim() != "")
                {
                    mysql = "select " + sttop + stCampouno.Text + "," + stCampocuatro.Text + "," + stCampodos.Text + "," + stCampotres.Text + " from " + stNombretabla.Text
                        + (Str_codAso == null ? "" : " where codigoter = '" + Str_codAso + "'")
                        + (stMysql2 == null ? " " : " where " + stMysql2);
                }
                else
                {
                    mysql = "select " + sttop + stCampouno.Text + "," + stCampocuatro.Text + "," + stCampodos.Text + " from " + stNombretabla.Text
                        + (Str_codAso == null ? "" : " where codigoter = '" + Str_codAso + "'")
                        + (stMysql2 == null ? " " : " where " + stMysql2);
                }
                buscar(mysql + stRowNum + " order by " + stCampouno.Text + " " + stlimit);
                return;
            }

            if (valor1.Text == "" && valor2.Text == "" && valor3.Text == "" && txtValor4.Text == "")
            {
                if (stCampotres.Text.Trim() != "")
                {
                    mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + "," + stCampotres.Text + " from " + stNombretabla.Text
                        + (Str_codAso == null ? "" : " where codigoter = '" + Str_codAso + "'")
                        + (stMysql2 == null ? " " : " where " + stMysql2);
                }
                else
                {
                    mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + " from " + stNombretabla.Text
                        + (Str_codAso == null ? "" : " where codigoter = '" + Str_codAso + "'")
                        + (stMysql2 == null ? " " : " where " + stMysql2);
                }
                buscar(mysql + stRowNum + " order by " + stCampouno.Text + " " + stlimit);
                return;
            }

            if (valor1.Text != "")
            {
                if (ComboBox2.Text == ComboBox2.Items[0].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text) + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampouno.Text + " like '"
                            + valor1.Text + "%' "
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampouno.Text + " like '"
                            + valor1.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
                if (ComboBox2.Text == ComboBox2.Items[1].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text)
                            + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampouno.Text + " like '%"
                            + valor1.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampouno.Text + " like '%"
                            + valor1.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
                if (ComboBox2.Text == ComboBox2.Items[2].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text) + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampouno.Text + " = '"
                            + valor1.Text + "'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampouno.Text + " = '"
                            + valor1.Text + "'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
            }

            if (valor2.Text != "")
            {
                if (ComboBox1.Text == ComboBox1.Items[0].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text) + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampodos.Text + " like '"
                            + valor2.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampodos.Text + " like '"
                            + valor2.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
                if (ComboBox1.Text == ComboBox1.Items[1].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text) + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampodos.Text + " like '%"
                            + valor2.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampodos.Text + " like '%"
                            + valor2.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
                if (ComboBox1.Text == ComboBox1.Items[2].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text) + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampodos.Text + " = '"
                            + valor2.Text + "'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampodos.Text + " = '"
                            + valor2.Text + "'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
            }

            if (valor3.Text != "")
            {
                if (ComboBox3.Text == ComboBox3.Items[0].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text) + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampotres.Text + " like '"
                            + valor3.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampotres.Text + " like '"
                            + valor3.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
                if (ComboBox3.Text == ComboBox3.Items[1].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text) + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampotres.Text + " like '%"
                            + valor3.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampotres.Text + " like '%"
                            + valor3.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
                // NOTE: VB original uses ComboBox2.Items(2) here (not ComboBox3) - preserved as-is
                if (ComboBox2.Text == ComboBox2.Items[2].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + (this.stNombretabla.Text == "sys_maenit" ? this.stCampocuatro.Text + "," + this.stCampodos.Text : this.stCampodos.Text) + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampotres.Text + " = '"
                            + valor3.Text + "'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampotres.Text + " = '"
                            + valor3.Text + "'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
            }

            if (txtValor4.Text != "")
            {
                if (ComboBox4.Text == ComboBox4.Items[0].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampocuatro.Text + "," + stCampodos.Text + stCampodosA + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampocuatro.Text + " like '"
                            + txtValor4.Text + "%' "
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampocuatro.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampocuatro.Text + " like '"
                            + txtValor4.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
                if (ComboBox4.Text == ComboBox4.Items[1].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampocuatro.Text + "," + stCampodos.Text + stCampodosA
                            + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampocuatro.Text + " like '%"
                            + txtValor4.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampocuatro.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampocuatro.Text + " like '%"
                            + txtValor4.Text + "%'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
                if (ComboBox4.Text == ComboBox4.Items[2].ToString())
                {
                    if (stCampotres.Text.Trim() != "")
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampocuatro.Text + "," + stCampodos.Text + stCampodosA + "," + stCampotres.Text
                            + " from " + stNombretabla.Text + " where " + stCampocuatro.Text + " = '"
                            + txtValor4.Text + "'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                    else
                    {
                        mysql = "select " + sttop + stCampouno.Text + "," + stCampocuatro.Text + "," + stCampodos.Text + stCampodosA
                            + " from " + stNombretabla.Text + " where " + stCampocuatro.Text + " = '"
                            + txtValor4.Text + "'"
                            + (Str_codAso == null ? "" : " and codigoter = '" + Str_codAso + "'")
                            + (stMysql2 == null ? " " : " and " + stMysql2);
                    }
                }
            }

            if (mysql != "")
            {
                if (txtValor4.Text.Trim() != "")
                {
                    buscar(mysql + stRowNum + " order by " + stCampocuatro.Text + " " + stlimit);
                }
                else
                {
                    buscar(mysql + stRowNum + " order by " + stCampouno.Text + " " + stlimit);
                }
            }
        }

        private void buscar(string stmysql)
        {
            int AnchoVentana = 0, i = 0;
            DataSet ds = new DataSet("Datos");
            this.lis3.Columns.Clear();
            ds.Tables.Add("Busca");
            OdbcDataAdapter da = new OdbcDataAdapter(stmysql, pmyConecayuda);
            try
            {
                if (da.Fill(ds, 0, Convert.ToInt32(this.limit.Text), "Busca") != 0)
                {
                    boEncontro = true;
                    this.lis3.DataSource = ds.Tables[0];
                }

                if (boEncontro == true)
                {
                    for (i = 0; i < lis3.Columns.Count; i++)
                    {
                        switch (i)
                        {
                            case 0:
                                lis3.Columns[i].HeaderText = this.campo1.Text;
                                lis3.Columns[i].SortMode = DataGridViewColumnSortMode.Automatic;
                                break;
                            case 1:
                                if (this.stNombretabla.Text == "sys_maenit")
                                {
                                    lis3.Columns[i].HeaderText = this.campo4.Text;
                                    lis3.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                                    AnchoVentana += lis3.Columns[i].Width;
                                    lis3.Columns[i].HeaderText = this.campo2.Text;
                                    i = i + 1;
                                }
                                else if (stCampocuatro.Text.Trim() != "" && this.txtValor4.Text.Trim() != "")
                                {
                                    lis3.Columns[i].HeaderText = this.campo4.Text;
                                    lis3.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                                    AnchoVentana += lis3.Columns[i].Width;
                                    lis3.Columns[i].HeaderText = this.campo2.Text;
                                    i = i + 1;
                                }
                                break;
                            case 2:
                            case 3:
                                if (i < lis3.Columns.Count)
                                {
                                    lis3.Columns[i].HeaderText = this.campo3.Text;
                                }
                                break;
                        }
                        if (i < lis3.Columns.Count)
                        {
                            lis3.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                            AnchoVentana += lis3.Columns[i].Width;
                        }
                    }
                    AnchoVentana = lis3.Width - AnchoVentana;
                    if (AnchoVentana > 0 && i != 0)
                    {
                        lis3.Columns[lis3.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                        lis3.Columns[lis3.Columns.Count - 1].Width += AnchoVentana;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + varini.pstBdatos + " Descripcion:" + ex.Message);
            }
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            if (stCampotres.Text == "")
            {
                ComboBox3.Visible = false;
                valor3.Visible = false;
            }
            if (stCampocuatro.Text == "" && this.stNombretabla.Text != "sys_maenit")
            {
                ComboBox4.Visible = false;
                txtValor4.Visible = false;
                campo4.Text = "";
            }
            boEncontro = false;
            valor1.Text = "";
            valor2.Text = "";
            valor3.Text = "";
            txtValor4.Text = "";
            ComboBox1.Text = ComboBox1.Items[0].ToString();
            ComboBox2.Text = ComboBox2.Items[0].ToString();
            ComboBox3.Text = ComboBox3.Items[0].ToString();
            ComboBox4.Text = ComboBox3.Items[1].ToString();
            limit.Text = limit.Items[0].ToString();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void valor1_TextChanged(object sender, EventArgs e)
        {
            valor2.Text = "";
            valor3.Text = "";
            txtValor4.Text = "";
        }

        private void valor2_TextChanged(object sender, EventArgs e)
        {
            valor1.Text = "";
            valor3.Text = "";
            txtValor4.Text = "";
        }

        private void valor3_TextChanged(object sender, EventArgs e)
        {
            valor1.Text = "";
            valor2.Text = "";
            txtValor4.Text = "";
        }

        private void lis3_DoubleClick(object sender, EventArgs e)
        {
            if (stRespu.Trim() != "")
            {
                this.Close();
            }
        }

        private void lis3_SelectionChanged(object sender, EventArgs e)
        {
            int indice;
            if (lis3.SelectedRows.Count != 0)
            {
                indice = lis3.SelectedRows[0].Index;
                stRespu = Str_codAso == null
                    ? Convert.ToString(lis3.Rows[indice].Cells[0].Value)
                    : Convert.ToString(lis3.Rows[indice].Cells[0].Value) + "," + Convert.ToString(lis3.Rows[indice].Cells[1].Value);
                stRes1 = Convert.ToString(lis3.Rows[indice].Cells[0].Value);
                stRes2 = Convert.ToString(lis3.Rows[indice].Cells[1].Value);
                Ok.Enabled = true;
            }
        }

        private void txtValor4_TextChanged(object sender, EventArgs e)
        {
            valor1.Text = "";
            valor2.Text = "";
            valor3.Text = "";
        }

        private void lis3_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void limit_SelectedIndexChanged(object sender, EventArgs e)
        {
        }
    }
}
