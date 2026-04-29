using ERP.Core.Compartido.Configuracion;
using System;
using System.ComponentModel;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    // Traducción de: Public Class Ordencomercio (Forms\Ordencomercio.vb)
    public partial class Ordencomercio : Form
    {
        private ERP.Core.Compartido.Utilidades.Ayuda MsgAyu = new ERP.Core.Compartido.Utilidades.Ayuda("");
        private ERP.Core.Contabilidad.Models.ParamCnt paramcop = new ERP.Core.Contabilidad.Models.ParamCnt();
        public OdbcConnection conect = new OdbcConnection();

        // Propiedades públicas (equivalente a las variables del módulo VarIni)
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Pstcodigo { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Pstnombre { get; set; }

        public Ordencomercio()
        {
            InitializeComponent();
        }

        private void Ordencomercio_Load(object sender, EventArgs e)
        {
            if (conect.State == ConnectionState.Closed)
            {
                conect.ConnectionString = VarIni.pstMyconec;
                conect.Open();
            }
        }

        private void me_FormClosing(object sender, FormClosingEventArgs e)
        {
            // conect.Close();
            // conect.Dispose();
        }

        private void ayuda_codi_Click(object sender, EventArgs e)
        {
            // prove.Text = MsgAyu.CargaAyuda("cnt_nit", "nit", "nombre", "", conect, this, "Razon social", "");
            prove.Text = paramcop.HelpNits(conect, this);
            prove.Focus();
        }

        private void prove_TextChanged(object sender, EventArgs e)
        {
            carga();
        }

        public void carga()
        {
            nombreprove.Text = " ";
            if (prove.Text.Trim() != "")
            {
                if (conect.State == ConnectionState.Closed)
                {
                    conect.ConnectionString = VarIni.pstMyconec;
                    conect.Open();
                }
                string _nit = prove.Text;
                string _nombre = " ";
                //paramcop.BuscarTercero(ref _nit, conect, ref _nombre);
                prove.Text = _nit;
                nombreprove.Text = _nombre;
            }
        }

        private void ok_Click(object sender, EventArgs e)
        {
           // VarIni.membrete = membrete.Checked;
            Pstcodigo = prove.Text;
            Pstnombre = nombreprove.Text;
            this.Close();
        }
    }
}
