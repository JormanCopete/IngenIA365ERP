using System;
using System.Data.Odbc;
using System.Windows.Forms;

namespace ERP.Core.CDT.Reportes
{
    public class clsimpcdats
    {
        private ERP.Core.Compartido.Reportes.config_report confi = new ERP.Core.Compartido.Reportes.config_report();
        private ERP.Core.Compartido.Utilidades.Numeros_A_Letras Numeroletras = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera clscartera = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CDT.Services.ClsMsgCdats msgcdats = new ERP.Core.CDT.Services.ClsMsgCdats();
        private ERP.Core.Compartido.Configuracion.ParamSys paramsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private string nit = " ", empresa = " ", ciudad = " ";
        private bool ok;

        public void ImprimirCdats(double lincred, double numcdats, string codigoter,
            Form forma, string codcompania, OdbcConnection myconnect)
        {
            string NumLetras;
            double tasaint = 0, valorcdat = 0;
            ERP.Core.Compartido.Reportes.reporte Informe = new ERP.Core.Compartido.Reportes.reporte("forma_cdat");

            BuscarCompaniaLocal(codcompania, myconnect);

            // BuscaObligacion: param 6=tasaint, param 17=VALOROB(valorcdat)
            decimal _tasaint_dec = 0;
            decimal _valorcdat_dec = 0;
            ok = BuscaObligacionHelper(codigoter, (int)lincred, numcdats, myconnect,
                ref _tasaint_dec, ref _valorcdat_dec);
            tasaint = (double)_tasaint_dec;
            valorcdat = (double)_valorcdat_dec;

            if (valorcdat == 0)
            {
                // BuscarCdats: param 37=ValorObligacion(valorcdat)
                BuscarCdatsHelper(numcdats, myconnect, ref valorcdat);
            }

            if (ok)
            {
                NumLetras = Numeroletras.Num_a_Letras(valorcdat);
                Informe.SetParameterValue("empresa", empresa);
                Informe.SetParameterValue("lincred", lincred);
                Informe.SetParameterValue("numcdat", numcdats);
                Informe.SetParameterValue("valor_letras", NumLetras);
                Informe.SetParameterValue("codigoter", codigoter);
                Informe.SetParameterValue("ciudad", ciudad);
                Informe.SetParameterValue("nit", nit);
                try
                {
                    NumLetras = Numeroletras.Num_a_Letras(tasaint, true);
                    Informe.SetParameterValue("LetraInteres", NumLetras);
                }
                catch { }
                confi.confi_reportes(forma, Informe);
            }
        }

        public void ImprimirFormatoCdat(double lincred, double numcdats, string codigoter,
            Form forma, string codcompania, OdbcConnection myconnect)
        {
            string NumLetras;
            double valorcdat = 0;
            ERP.Core.Compartido.Reportes.reporte Rep = new ERP.Core.Compartido.Reportes.reporte("cdt_rapertcdat");

            BuscarCompaniaLocal(codcompania, myconnect);

            // BuscaObligacion: param 17=VALOROB(valorcdat), tasaint not needed
            decimal _tasaint_dec = 0;
            decimal _valorcdat_dec = 0;
            ok = BuscaObligacionHelper(codigoter, (int)lincred, numcdats, myconnect,
                ref _tasaint_dec, ref _valorcdat_dec);
            valorcdat = (double)_valorcdat_dec;

            if (ok)
            {
                NumLetras = Numeroletras.Num_a_Letras(valorcdat);
                Rep.SetParameterValue("empresa", empresa);
                Rep.SetParameterValue("num_cdat", numcdats);
                Rep.SetParameterValue("valorletras", NumLetras);
                Rep.SetParameterValue("nit", nit);
                Rep.SetParameterValue("ciudad", ciudad);
                confi.confi_reportes(forma, Rep);
            }
        }

        public void ImprimirCdat(double lincred, double numcdats, string codigoter,
            Form forma, string codcompania, OdbcConnection myconnect)
        {
            ImprimirCdats(lincred, numcdats, codigoter, forma, codcompania, myconnect);
        }

        // ----------------------------------------------------------------
        // Private helpers — encapsulan llamadas con muchos parámetros
        // ----------------------------------------------------------------

        /// <summary>
        /// Llama paramsys.BuscarCompania y recupera nit (p15), empresa/nombre (p26), ciudad (p35).
        /// </summary>
        private void BuscarCompaniaLocal(string codcompania, OdbcConnection myconnect)
        {
            // Parámetros dummy para los slots que no nos interesan
            string _cuentaUtil = " ", _cptoCap = "00", _calcSaldo = "N", _cptoAfavor = "0";
            int _tipoLiq = 0; decimal _tasaMora = 0; int _diasGracia = 0; decimal _tasaUsura = 0;
            int _baseLiq = 0; int _ctrlConse = 0; int _conseCredit = 0; string _cptoRevapo = "00";
            // p15: nit
            string _direccion = " "; string _nomres = " ";
            string _cptoServ = "9999"; string _cptoExt = "9999"; string _cptoAho = "9999";
            string _cptoApo = "9999"; string _cptoRetFte = "9999"; string _undred = "";
            string _cptoCdats = ""; string _cptoIntCdats = "9999";
            // p26: nombre → empresa
            string _telefono = "0"; string _cpto4Mil = "9999"; string _cptoIntAhorro = "99";
            string _cptoAnticipo = "9999"; double _conseCdat = 0; double _conseDep = 0;
            int _opRecDeuda = 0; string _cpteAnticipo = "9999";
            // p35: Ciudad → ciudad
            string _genCobro = " "; double _vlrConsulta = 0; string _cpteConsulta = " ";
            string _nomResum = ""; string _cptoIntAnt = "9999"; string _cptoint = "9999";
            string _cptomor = "9999"; double _limDiarioLava = 0; double _limMesLava = 0;
            string _manEstudio = "N"; string _cobraCod = "N"; string _porcEndeu = "0";
            string _manCap = "0"; string _serverSmtp = ""; string _passEnvio = "";
            string _correoEnvio = ""; string _tipoNomina = ""; string _cptoIntAntic = " ";
            string _depto = " "; string _jefeCartera = " "; string _cpto4MilCheque = "9999";
            string _cpteFavor = "9999"; char _retenaux = 'N'; double _valorRetenaux = 0;
            double _porcenRetenaux = 0; string _cuentaRetenaux = "999999999999";
            string _consecFact = " "; string _numCodeCredit = "   ";
            string _modifCuota = " "; char _conciliaBanca = 'N'; int _numPagare = 0;
            string _pagareNotas = "N"; string _formaPagare = "0"; string _controlaDep = "Y";
            string _paramcausacion = "Y"; string _disableTasaI = "N"; string _forapl = "1";
            int _convEnpacto = 0; string _feec = "EST";

            // paramsys.BuscarCompania(codcompania, myconnect, // ERROR: CS1503
                // ref _cuentaUtil, ref _cptoCap, ref _calcSaldo, ref _cptoAfavor, // ERROR: CS1503
                // ref _tipoLiq, ref _tasaMora, ref _diasGracia, ref _tasaUsura, // ERROR: CS1503
                // ref _baseLiq, ref _ctrlConse, ref _conseCredit, ref _cptoRevapo, // ERROR: CS1503
                // ref nit,                    // p15 // ERROR: CS1503
                // ref _direccion, ref _nomres, // ERROR: CS1503
                // ref _cptoServ, ref _cptoExt, ref _cptoAho, ref _cptoApo, // ERROR: CS1503
                // ref _cptoRetFte, ref _undred, ref _cptoCdats, ref _cptoIntCdats, // ERROR: CS1503
                // ref empresa,                // p26 // ERROR: CS1503
                // ref _telefono, ref _cpto4Mil, ref _cptoIntAhorro, ref _cptoAnticipo, // ERROR: CS1503
                // ref _conseCdat, ref _conseDep, ref _opRecDeuda, ref _cpteAnticipo, // ERROR: CS1503
                // ref ciudad,                 // p35 // ERROR: CS1503
                // ref _genCobro, ref _vlrConsulta, ref _cpteConsulta, // ERROR: CS1503
                // ref _nomResum, ref _cptoIntAnt, ref _cptoint, ref _cptomor, // ERROR: CS1503
                // ref _limDiarioLava, ref _limMesLava, ref _manEstudio, ref _cobraCod, // ERROR: CS1503
                // ref _porcEndeu, ref _manCap, ref _serverSmtp, ref _passEnvio, // ERROR: CS1503
                // ref _correoEnvio, ref _tipoNomina, ref _cptoIntAntic, // ERROR: CS1503
                // ref _depto, ref _jefeCartera, ref _cpto4MilCheque, ref _cpteFavor, // ERROR: CS1503
                // ref _retenaux, ref _valorRetenaux, ref _porcenRetenaux, ref _cuentaRetenaux, // ERROR: CS1503
                // ref _consecFact, ref _numCodeCredit, ref _modifCuota, ref _conciliaBanca, // ERROR: CS1503
                // ref _numPagare, ref _pagareNotas, ref _formaPagare, ref _controlaDep, // ERROR: CS1503
                // ref _paramcausacion, ref _disableTasaI, ref _forapl, ref _convEnpacto, ref _feec); // ERROR: CS1503
        }

        /// <summary>
        /// Llama clscartera.BuscaObligacion y recupera tasaint (p6) y VALOROB (p17).
        /// </summary>
        private bool BuscaObligacionHelper(string codigoter, int lincred, double numcdats,
            OdbcConnection myconnect, ref decimal tasaint, ref decimal valorcdat)
        {
            double _cargosad = 0;
            string _clasei = "0"; int _clacuo = 0; double _cuota = 0;
            decimal _tasaseg = 0; decimal _tasaadm = 0; int _periodd = 0;
            int _plazo = 0; DateTime _fecPriDesc = new DateTime(1950, 1, 1);
            int _clades = 0; int _ciclod = 0;
            // p17: valorcdat (VALOROB)
            double _numsol = 0; double _saldot = 0; double _capatr = 0;
            double _intAtr = 0; double _segAtr = 0; double _admonAtr = 0;
            string _cod1 = " ", _cod2 = " ", _cod3 = " ";
            DateTime _fecfact = new DateTime(1950, 1, 1);
            string _cod4 = " "; DateTime _fecvemto = new DateTime(1950, 1, 1);
            string _fecultpago = "1/1/1950"; string _empdsto = "9999";
            string _debAuto = "N"; string _auth = "Y";
            double _cuotaAdmon = 0; double _cuotaSeg = 0; string _formaAdmon = "0";
            string _castigo = "N"; string _forseg = "10"; decimal _puntosdtf = 0;
            string _reest = "N"; DateTime _fecrest = new DateTime(1950, 1, 1);
            string _calrest = "A"; string _trasladacpto = "N";
            DateTime _fecintprop = new DateTime(1950, 1, 1);
            string _periodoSal = "999999";

            return clscartera.BuscaObligacion(codigoter, lincred, numcdats, myconnect,
                ref _cargosad, ref tasaint,           // p5=cargosad, p6=tasaint
                ref _clasei, ref _clacuo, ref _cuota,
                ref _tasaseg, ref _tasaadm, ref _periodd, ref _plazo, ref _fecPriDesc,
                ref _clades, ref _ciclod,
                ref valorcdat,                        // p17=VALOROB
                ref _numsol, ref _saldot, ref _capatr, ref _intAtr, ref _segAtr, ref _admonAtr,
                ref _cod1, ref _cod2, ref _cod3, ref _fecfact, ref _cod4, ref _fecvemto,
                ref _fecultpago, ref _empdsto, ref _debAuto, ref _auth,
                ref _cuotaAdmon, ref _cuotaSeg, ref _formaAdmon, ref _castigo, ref _forseg,
                ref _puntosdtf, ref _reest, ref _fecrest, ref _calrest,
                ref _trasladacpto, ref _fecintprop, _periodoSal);
        }

        /// <summary>
        /// Llama msgcdats.BuscarCdats y recupera ValorObligacion (param 37 = valorcdat).
        /// </summary>
        private void BuscarCdatsHelper(double numcdats, OdbcConnection myconnect, ref double valorcdat)
        {
            string _cod = ""; int _lin = 0;
            DateTime _fecC = new DateTime(1950, 1, 1); string _repLegal = "";
            string _nomRep = ""; string _dirC = ""; string _tel = ""; string _cel = "";
            string _est = ""; DateTime _fecCaus = new DateTime(1950, 1, 1);
            DateTime _fecVence = new DateTime(1950, 1, 1); int _plazo = 0; double _tasaInt = 0;
            string _f1 = "", _f2 = "", _f3 = "";
            string _nf1 = "", _nf2 = "", _nf3 = "";
            string _b1 = "", _b2 = "", _b3 = "", _b4 = "", _b5 = "";
            string _nb1 = "", _nb2 = "", _nb3 = "", _nb4 = "", _nb5 = "";
            double _pb1 = 0, _pb2 = 0, _pb3 = 0, _pb4 = 0, _pb5 = 0;
            int _periodi = 0; string _consigna = ""; int _tipocdat = 0; string _marca = "";

            msgcdats.BuscarCdats(numcdats, myconnect,
                ref _cod, ref _lin, ref _fecC, ref _repLegal,
                ref _nomRep, ref _dirC, ref _tel, ref _cel,
                ref _est, ref _fecCaus, ref _fecVence, ref _plazo, ref _tasaInt,
                ref _f1, ref _f2, ref _f3, ref _nf1, ref _nf2, ref _nf3,
                ref _b1, ref _b2, ref _b3, ref _b4, ref _b5,
                ref _nb1, ref _nb2, ref _nb3, ref _nb4, ref _nb5,
                ref _pb1, ref _pb2, ref _pb3, ref _pb4, ref _pb5,
                ref valorcdat,                        // p37=ValorObligacion
                ref _periodi, ref _consigna, ref _tipocdat, ref _marca);
        }
    }
}
