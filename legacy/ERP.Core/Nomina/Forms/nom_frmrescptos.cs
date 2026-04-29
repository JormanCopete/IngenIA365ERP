using System;
using ERP.Core.Nomina.Services;
using System.Data;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Nomina.Forms
{
    public partial class nom_frmrescptos : Form
    {
        private ERP.Core.Nomina.Services.msgnomconfig msgsysnom = new ERP.Core.Nomina.Services.msgnomconfig();
        private msgnom msgnom = new msgnom();
        private ERP.Core.Compartido.Configuracion.ParamSys msgsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.Nomina.Reportes.clsmsgimp msgimp = new ERP.Core.Nomina.Reportes.clsmsgimp();
        private DataTable DsDataSet = new DataTable();
        public System.Data.Odbc.OdbcConnection myconnect = new System.Data.Odbc.OdbcConnection();

        private void nom_frmrescptos_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            msgsys.ConfiguraForma(this, this.TsysEmpresa.Text, this.TsysServer.Text, this.TsysBd.Text, this.TsysPrograName.Text, this.TsysUsuario.Text, this.TsysFechaNow.Text);
            // msgsysnom.buscaPeriodo(myconnect, "", "", DateTime.Now, "", this.Tsysperiodo.Text, DateTime.Now.Year); // ERROR: CS1503, CS1620
            CargaAcumulados();
        }

        private void CargaAcumulados()
        {
            int fila = 0;
            double Devengos = 0, Deduciones = 0;

            DsDataSet = msgnom.HelpAcumuladosCpto(Convert.ToInt32(this.TxtIdEmpresa.Text), Convert.ToDouble(this.TxtidCodigo.Text), myconnect);
            this.DtgPrestLib.AutoGenerateColumns = false;
            this.DtgPrestLib.DataSource = DsDataSet;

            for (fila = 0; fila <= DsDataSet.Rows.Count - 1; fila++)
            {
                Devengos += Convert.ToDouble(DsDataSet.Rows[fila]["Devengo"]);
                Deduciones += Convert.ToDouble(DsDataSet.Rows[fila]["Deduccion"]);
            }

            this.TxtDevengos.Text = Strings.FormatNumber(Devengos, 0);
            this.TxtDeducciones.Text = Strings.FormatNumber(Deduciones, 0);
        }

        private void CmbSalir_Click(object sender, EventArgs e)
        {
            Salir();
        }

        private void Salir()
        {
            this.Close();
            this.Dispose();
        }

        private void CmbAceptar_Click(object sender, EventArgs e)
        {
            msgimp.ImprimeAcumulados(Convert.ToInt32(this.TxtIdEmpresa.Text), Convert.ToDouble(this.TxtidCodigo.Text), this, this.myconnect);
        }
    }
}
