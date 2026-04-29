using ERP.Core.CarteraFinanciera.Models;
using ERP.Core.Compartido.Utilidades;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Configuracion
{
    public partial class FrmActividades : Form
    {
        private ParamCop paramcop = new ParamCop();
        private ERP.Core.Compartido.Utilidades.Ayuda msgsas = new ERP.Core.Compartido.Utilidades.Ayuda(VarIni.pstUsuario);
        private OdbcConnection conexion;
        private DataSet dataset = new DataSet();
        public DataSet DsActividades = new DataSet();
        private string StTabla = "";
        private double porcentaje = 0;
        private string NomTipoActividad = " ";
        private DateTime FecInicio = new DateTime(1950, 1, 1);
        private DateTime FecFin = new DateTime(1950, 1, 1);

        public FrmActividades(OdbcConnection myconnect)
        {
            InitializeComponent();
            this.conexion = myconnect;
        }

        private void FrmActividades_Load(object sender, EventArgs e)
        {
            ConfiguraGrilla();
            DatosAsociado();
            switch (TxtTipoActividad.Text)
            {
                case "1":
                    StTabla = "TblCultural";
                    CargarActividadesCulturales();
                    break;
                case "2":
                    StTabla = "TblDeportiva";
                    CargarActividadesDeportivas();
                    break;
                case "3":
                    StTabla = "TblCursos";
                    CargarCursos();
                    break;
                case "4":
                    StTabla = "TblRecreacion";
                    CargarRecreacion();
                    break;
            }
        }

        private void DatosAsociado()
        {
            LblNomAsociado.Text = " ";
            string nombre = "";
            paramcop.BuscarAsociado(TxtCodigoter.Text, conexion, null,ref nombre);
            LblNomAsociado.Text = nombre;
        }

        private void CargarActividadesCulturales()
        {
            if (DsActividades.Tables.Contains("TblCultural"))
            {
                dataset.Tables.Add(DsActividades.Tables["TblCultural"].Copy());
            }
            else
            {
                dataset = paramcop.ActividadesAsociado(TxtCodigoter.Text, conexion, ParamCop.actividad.cultural);
                if (dataset.Tables.Contains("TblCultural"))
                    DsActividades.Tables.Add(dataset.Tables["TblCultural"].Copy());
            }
            if (dataset.Tables.Contains("TblCultural"))
                DgwActividades.DataSource = dataset.Tables["TblCultural"];
            TxtBeneficiario.Text = "999999999999";
            TxtBeneficiario.Enabled = false;
            HelpBeneficiario.Enabled = false;
        }

        private void CargarActividadesDeportivas()
        {
            if (DsActividades.Tables.Contains("TblDeportiva"))
            {
                dataset.Tables.Add(DsActividades.Tables["TblDeportiva"].Copy());
            }
            else
            {
                dataset = paramcop.ActividadesAsociado(TxtCodigoter.Text, conexion, ParamCop.actividad.deportiva);
                if (dataset.Tables.Contains("TblDeportiva"))
                    DsActividades.Tables.Add(dataset.Tables["TblDeportiva"].Copy());
            }
            if (dataset.Tables.Contains("TblDeportiva"))
                DgwActividades.DataSource = dataset.Tables["TblDeportiva"];
            TxtBeneficiario.Text = "999999999999";
            TxtBeneficiario.Enabled = false;
            HelpBeneficiario.Enabled = false;
        }

        private void CargarCursos()
        {
            if (DsActividades.Tables.Contains("TblCursos"))
            {
                dataset.Tables.Add(DsActividades.Tables["TblCursos"].Copy());
            }
            else
            {
                dataset = paramcop.ActividadesAsociado(TxtCodigoter.Text, conexion, ParamCop.actividad.curso);
                if (dataset.Tables.Contains("TblCursos"))
                    DsActividades.Tables.Add(dataset.Tables["TblCursos"].Copy());
            }
            if (dataset.Tables.Contains("TblCursos"))
                DgwActividades.DataSource = dataset.Tables["TblCursos"];
            TxtBeneficiario.Text = "";
            TxtBeneficiario.Enabled = true;
            HelpBeneficiario.Enabled = true;
        }

        private void CargarRecreacion()
        {
            if (DsActividades.Tables.Contains("TblRecreacion"))
            {
                dataset.Tables.Add(DsActividades.Tables["TblRecreacion"].Copy());
            }
            else
            {
                dataset = paramcop.ActividadesAsociado(TxtCodigoter.Text, conexion, ParamCop.actividad.recreativa);
                if (dataset.Tables.Contains("TblRecreacion"))
                    DsActividades.Tables.Add(dataset.Tables["TblRecreacion"].Copy());
            }
            if (dataset.Tables.Contains("TblRecreacion"))
                DgwActividades.DataSource = dataset.Tables["TblRecreacion"];
            TxtBeneficiario.Text = "";
            TxtBeneficiario.Enabled = true;
            HelpBeneficiario.Enabled = true;
        }

        private void ConfiguraGrilla()
        {
            DgwActividades.DataSource = null;
            DgwActividades.AutoGenerateColumns = false;
            DgwActividades.Columns.Clear();

            switch (TxtTipoActividad.Text)
            {
                case "1":
                case "2":
                    DgwActividades.Columns.Add("clmCodActividad", "Codigo");
                    DgwActividades.Columns.Add("clmDescripcion", "Descripcion");
                    DgwActividades.Columns.Add("clmFecha", "Fecha");
                    DgwActividades.Columns.Add("clmParticipante", "Cod. Participante");
                    DgwActividades.Columns.Add("clmobservacion", "Observacion");
                    DgwActividades.Columns[0].Width = 80;
                    DgwActividades.Columns[0].DataPropertyName = "Codigo_actividad";
                    DgwActividades.Columns[1].Width = 300;
                    DgwActividades.Columns[1].DataPropertyName = "nombre";
                    DgwActividades.Columns[2].Width = 120;
                    DgwActividades.Columns[2].DataPropertyName = "fecingreso";
                    DgwActividades.Columns[3].Width = 120;
                    DgwActividades.Columns[3].DataPropertyName = "CodParticipante";
                    DgwActividades.Columns[4].Width = 200;
                    DgwActividades.Columns[4].DataPropertyName = "observacion";
                    break;
                case "3":
                    DgwActividades.Columns.Add("clmCodActividad", "Codigo");
                    DgwActividades.Columns.Add("clmDescripcion", "Descripcion");
                    DgwActividades.Columns.Add("clmFecha", "Fecha");
                    DgwActividades.Columns.Add("clmParticipante", "Cod. Participante");
                    DgwActividades.Columns.Add("clmNomParticipante", "Nombre");
                    DgwActividades.Columns.Add("clmPorcentaje", "%");
                    DgwActividades.Columns.Add("clmobservacion", "Observacion");
                    DgwActividades.Columns[0].Width = 50;
                    DgwActividades.Columns[0].DataPropertyName = "Codigo_actividad";
                    DgwActividades.Columns[1].Width = 150;
                    DgwActividades.Columns[1].DataPropertyName = "nombre";
                    DgwActividades.Columns[2].Width = 70;
                    DgwActividades.Columns[2].DataPropertyName = "fecingreso";
                    DgwActividades.Columns[3].Width = 110;
                    DgwActividades.Columns[3].DataPropertyName = "CodParticipante";
                    DgwActividades.Columns[4].Width = 200;
                    DgwActividades.Columns[4].DataPropertyName = "NombreParticipante";
                    DgwActividades.Columns[5].Width = 60;
                    DgwActividades.Columns[5].DataPropertyName = "porcentaje";
                    DgwActividades.Columns[6].Width = 200;
                    DgwActividades.Columns[6].DataPropertyName = "observacion";
                    break;
                case "4":
                    DgwActividades.Columns.Add("clmCodActividad", "Codigo");
                    DgwActividades.Columns.Add("clmDescripcion", "Descripcion");
                    DgwActividades.Columns.Add("clmFecha", "Fecha");
                    DgwActividades.Columns.Add("clmParticipante", "Cod. Participante");
                    DgwActividades.Columns.Add("clmNomParticipante", "Nombre");
                    DgwActividades.Columns.Add("clmTipoAct", "Tipo Act");
                    DgwActividades.Columns.Add("clmFecInicio", "Fec. Inicio");
                    DgwActividades.Columns.Add("clmFecFin", "Fec. Fin");
                    DgwActividades.Columns.Add("clmPorcentaje", "%");
                    DgwActividades.Columns[0].Width = 50;
                    DgwActividades.Columns[0].DataPropertyName = "Codigo_actividad";
                    DgwActividades.Columns[1].Width = 150;
                    DgwActividades.Columns[1].DataPropertyName = "detalle";
                    DgwActividades.Columns[2].Width = 70;
                    DgwActividades.Columns[2].DataPropertyName = "fecingreso";
                    DgwActividades.Columns[3].Width = 120;
                    DgwActividades.Columns[3].DataPropertyName = "CodParticipante";
                    DgwActividades.Columns[4].Width = 200;
                    DgwActividades.Columns[4].DataPropertyName = "NombreParticipante";
                    DgwActividades.Columns[5].Width = 80;
                    DgwActividades.Columns[5].DataPropertyName = "TipoActividad";
                    DgwActividades.Columns[6].Width = 90;
                    DgwActividades.Columns[6].DataPropertyName = "fecha_inicio";
                    DgwActividades.Columns[7].Width = 80;
                    DgwActividades.Columns[7].DataPropertyName = "fecha_termino";
                    DgwActividades.Columns[8].Width = 60;
                    DgwActividades.Columns[8].DataPropertyName = "porcentaje";
                    break;
            }
        }

        private void HelpBeneficiario_Click(object sender, EventArgs e)
        {
            TxtBeneficiario.Text = paramcop.HelpPersonaCargo(conexion, this, TxtCodigoter.Text);
            TxtBeneficiario.Focus();
        }

        private void TxtBeneficiario_Leave(object sender, EventArgs e)
        {
            LblNomBeneficiario.Text = " ";
            if (TxtBeneficiario.Text.Trim() != "" && TxtCodigoter.Text.Trim() != "")
            {
                string nombre = "";
                paramcop.BuscaBeneficiarioAsociado(TxtCodigoter.Text, TxtBeneficiario.Text, conexion, ref nombre);
                LblNomBeneficiario.Text = nombre;
            }
        }

        private void HelpActividad_Click(object sender, EventArgs e)
        {
            string respCampo = string.Empty;
            switch (TxtTipoActividad.Text)
            {
                case "1":
                    TxtActividad.Text = msgsas.CargaAyuda("sys_cultura54", "codigo", "nombre", "nomres", conexion, this, "Nombre", "Nombre resumido", null, null, ref respCampo);
                    break;
                case "2":
                    TxtActividad.Text = msgsas.CargaAyuda("sys_deport53", "codigo", "nombre", "nomres", conexion, this, "Nombre", "Nombre resumido", null, null, ref respCampo);
                    break;
                case "3":
                    TxtActividad.Text = msgsas.CargaAyuda("sys_curso", "codigo", "nombre", "nomres", conexion, this, "Nombre", "Nombre resumido", null, null, ref respCampo);
                    break;
                case "4":
                    TxtActividad.Text = msgsas.CargaAyuda("sys_recreacion", "codigo", "detalle", "case TipoActividad when '1' then 'AutoFinanciada' when '2' then 'Financiada' end as TipoActividad", conexion, this, "Descripcion", "Tipo Actividad",null,null, ref respCampo);
                    break;
            }
            TxtActividad.Focus();
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }

        private bool Validar()
        {
            bool ok;
            if (TxtActividad.Text.Trim() != "")
            {
                switch (TxtTipoActividad.Text)
                {
                    case "1":
                        ok = paramcop.BuscaActividad(TxtActividad.Text, conexion, ParamCop.actividad.cultural);
                        break;
                    case "2":
                        ok = paramcop.BuscaActividad(TxtActividad.Text, conexion, ParamCop.actividad.deportiva);
                        break;
                    case "3":
                        ok = paramcop.BuscarCursos(TxtActividad.Text, conexion);
                        break;
                    case "4":
                        ok = paramcop.BuscarRecreacion(TxtActividad.Text, conexion);
                        break;
                    default:
                        ok = false;
                        break;
                }
                if (!ok)
                {
                    MessageBox.Show("Actividad no existe, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    TxtActividad.Text = "";
                    LblNomActividad.Text = " ";
                    TxtActividad.Focus();
                    return false;
                }
            }
            else
            {
                MessageBox.Show("Debe digitar una actividad", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TxtActividad.Text = "";
                TxtActividad.Focus();
                return false;
            }

            if (TxtTipoActividad.Text == "3" || TxtTipoActividad.Text == "4")
            {
                if (TxtBeneficiario.Text.Trim() != "" && TxtBeneficiario.Text.Trim() != "999999999999")
                {
                    ok = paramcop.BuscaBeneficiarioAsociado(TxtCodigoter.Text, TxtBeneficiario.Text, conexion);
                    if (!ok)
                    {
                        MessageBox.Show("Beneficiario no existe, Por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        TxtBeneficiario.Text = "";
                        TxtBeneficiario.Focus();
                        return false;
                    }
                }
            }
            return true;
        }

        private void BtnAgregar_Click(object sender, EventArgs e)
        {
            if (Validar())
            {
                AgregarActividades();
                TxtActividad.Text = "";
                DtpFecha.Value = DateTime.Now;
                LblNomActividad.Text = " ";
                TxtObservaciones.Clear();
                if (TxtTipoActividad.Text == "3" || TxtTipoActividad.Text == "4")
                {
                    TxtBeneficiario.Text = "";
                    LblNomBeneficiario.Text = " ";
                }
            }
        }

        private void AgregarActividades()
        {
            string CodParticipante, NomParticipante;
            switch (TxtTipoActividad.Text)
            {
                case "1":
                case "2":
                    if (!paramcop.GrabarAficionesAsociado(TxtCodigoter.Text, TxtTipoActividad.Text, TxtActividad.Text, DtpFecha.Value, TxtBeneficiario.Text, TxtObservaciones.Text.Trim(), conexion))
                    {
                        MessageBox.Show("La aficion ya existe para este asociado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    DsActividades.Tables[StTabla].Rows.Add(TxtActividad.Text, LblNomActividad.Text, DtpFecha.Value, TxtBeneficiario.Text, TxtObservaciones.Text.Trim());
                    break;
                case "3":
                case "4":
                    if (TxtBeneficiario.Text.Trim() != "" && TxtBeneficiario.Text != "999999999999")
                    {
                        CodParticipante = TxtBeneficiario.Text;
                        NomParticipante = LblNomBeneficiario.Text;
                    }
                    else
                    {
                        CodParticipante = TxtCodigoter.Text;
                        NomParticipante = LblNomAsociado.Text;
                    }
                    if (TxtTipoActividad.Text == "3")
                    {
                        if (!paramcop.GrabarCursoAsociado(TxtCodigoter.Text, TxtTipoActividad.Text, TxtActividad.Text, DtpFecha.Value, CodParticipante, TxtObservaciones.Text.Trim(), conexion))
                        {
                            MessageBox.Show("El asociado ya esta inscrito a este curso", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        DsActividades.Tables[StTabla].Rows.Add(TxtActividad.Text, LblNomActividad.Text, DtpFecha.Value, CodParticipante, NomParticipante, porcentaje, TxtObservaciones.Text.Trim());
                    }
                    else
                    {
                        DsActividades.Tables[StTabla].Rows.Add(TxtActividad.Text, LblNomActividad.Text, DtpFecha.Value, CodParticipante, NomParticipante, NomTipoActividad, FecInicio, FecFin, porcentaje);
                    }
                    break;
            }
            DgwActividades.DataSource = DsActividades.Tables[StTabla];
            DgwActividades.Refresh();
        }

        private void TxtActividad_Leave(object sender, EventArgs e)
        {
            porcentaje = 0;
            LblNomActividad.Text = " ";
            string nombre = "";
            switch (TxtTipoActividad.Text)
            {
                case "1":
                    paramcop.BuscaActividad(TxtActividad.Text, conexion, ParamCop.actividad.cultural, ref nombre);
                    break;
                case "2":
                    paramcop.BuscaActividad(TxtActividad.Text, conexion, ParamCop.actividad.deportiva, ref nombre);
                    break;
                case "3":
                    paramcop.BuscarCursos(TxtActividad.Text, conexion, ref nombre, ref porcentaje);
                    break;
                case "4":
                    paramcop.BuscarRecreacion(TxtActividad.Text, conexion, ref nombre, ref FecInicio, ref FecFin, ref porcentaje, ref NomTipoActividad);
                    break;
            }
            LblNomActividad.Text = nombre;
        }

        private void BtnQuitar_Click(object sender, EventArgs e)
        {
            if (DgwActividades.SelectedCells.Count <= 0)
            {
                MessageBox.Show("Debe selecionar un registro", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Esta seguro de eliminar este registro?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }
            switch (TxtTipoActividad.Text)
            {
                case "1":
                case "2":
                    paramcop.EliminarAficionAsociado(TxtCodigoter.Text, DgwActividades.CurrentRow.Index, TxtTipoActividad.Text, DsActividades, conexion);
                    DsActividades.Tables[StTabla].Rows.RemoveAt(DgwActividades.CurrentRow.Index);
                    DgwActividades.DataSource = DsActividades.Tables[StTabla];
                    break;
                case "3":
                    paramcop.EliminarCursoIncrito(TxtCodigoter.Text, DgwActividades.CurrentRow.Index, DsActividades, conexion);
                    DsActividades.Tables[StTabla].Rows.RemoveAt(DgwActividades.CurrentRow.Index);
                    DgwActividades.DataSource = DsActividades.Tables[StTabla];
                    break;
            }
        }
    }
}
