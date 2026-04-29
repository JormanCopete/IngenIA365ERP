// Traducción de: Clspagonomi.vb (mscpagonomi)
using System;
using System.Data.Odbc;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Nomina.Services
{
    public class Clspagonomi
    {
        private ERP.Core.Compartido.Configuracion.ParamSys msgconfsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera clscartera = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private StreamReader strStreamReader;
        private StreamWriter strStreamWriter;
        private bool ok;

        public bool PlanoPagoNomina(string NombreArchivo, string Comprobante, double ConseCpte,
            DateTime FechaMovto, Form Myforma, string usuario, string CodCompania,
            OdbcConnection myconnect)
        {
            string Cedula = " ", valor = " ", NumCta = " ", nit = " ", lincred = "9999";
            string CptoAho = "9999", CptoCap = "9999";
            int sw1 = 0;
            double TotPag = 0;
            ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos msgdepInst = new ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos();

            // My.Application.Info.DirectoryPath → AppDomain.CurrentDomain.BaseDirectory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempDir = Path.Combine(baseDir, "Temporales");
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);

            string ruta = Path.Combine(tempDir, "PlanoPagoNomina.txt");
            strStreamWriter = new StreamWriter(ruta, false);

            // Read first line to calculate record count for progress bar
            // (replaces: strStreamReader.ReadLine + FileLen/Len)
            strStreamReader = new StreamReader(NombreArchivo);
            string line = strStreamReader.ReadLine();
            strStreamReader.Close();

            long fileLen = new FileInfo(NombreArchivo).Length;
            decimal Treg = (decimal)Math.Round((double)fileLen / (line.Length + 2));

            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Procesando Archivo", Myforma);
            BarraProgreso.ValorMinimoMaximo(0, (int)Treg);
            BarraProgreso.Show();

            // p4=CptoCap, p20=CptoAho
            BuscarCompaniaHelper(CodCompania, myconnect, ref CptoCap, ref CptoAho);
            sw1 = 0;

            // Replace VB6 FileOpen/Input(1,Cedula)/Input(1,valor)/EOF/FileClose
            // with StreamReader reading comma-delimited lines
            using (StreamReader reader = new StreamReader(NombreArchivo))
            {
                while (!reader.EndOfStream)
                {
                    string recordLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(recordLine)) continue;

                    string[] parts = recordLine.Split(',');
                    Cedula = parts.Length > 0 ? parts[0] : " ";
                    valor  = parts.Length > 1 ? parts[1] : " ";
                    NumCta = " ";

                    // p4=nit, p12=NumCta (Cuentabanco)
                    ok = BuscaAsociadoHelper(ref Cedula, myconnect, ref nit, ref NumCta);

                    if (!ok)
                    {
                        strStreamWriter.WriteLine(Cedula + " - Asociado no existe");
                        sw1 = 1;
                    }
                    else
                    {
                        if (NumCta.Trim() == "")
                        {
                            strStreamWriter.WriteLine(Cedula + " - Cedula no tiene una cuenta asignada en la hoja de vida");
                            sw1 = 1;
                        }
                        else if (!Information.IsNumeric(NumCta))
                        {
                            strStreamWriter.WriteLine(Cedula + " - La cuenta asignada en la hoja de vida para esta cedula no es numerica");
                            sw1 = 1;
                        }
                        else
                        {
                            string _codigoCta = " ";
                            ok = msgdepInst.BuscarCuentaAhorro(NumCta, myconnect,
                                global::ERP.Core.CarteraFinanciera.Services.Depositos.ClsDepositos.Navega.Ninguno,
                                ref _codigoCta, ref lincred);

                            if (!ok)
                            {
                                strStreamWriter.WriteLine(Cedula + " - La cuenta asignada en la hoja de vida para esta cedula no existe en maestro de cuentas");
                                sw1 = 1;
                            }
                            else
                            {
                                double dValor = 0;
                                double.TryParse(valor, out dValor);
                                int iLincred = 9999;
                                int.TryParse(lincred, out iLincred);
                                double dNumCta = 0;
                                double.TryParse(NumCta, out dNumCta);
                                int iPeriodo = int.Parse(FechaMovto.ToString("yyyyMM"));

                                GrabaMovimientoHelper(Comprobante, ConseCpte, Cedula,
                                    iLincred, dNumCta, iPeriodo, CptoAho, FechaMovto,
                                    ref dValor, nit, myconnect);

                                TotPag += dValor;
                            }
                        }
                    }
                    BarraProgreso.PerformStep();
                }
            }

            if (TotPag > 0)
                ok = clscartera.TrasladaContabilidad(Comprobante, ConseCpte, myconnect, usuario, Cedula);

            strStreamWriter.Close();
            strStreamWriter.Dispose();
            BarraProgreso.Close();
            BarraProgreso.Dispose();

            if (sw1 == 1)
            {
                // MsgBox with OkOnly always returns OK, so Process.Start always executes
                MessageBox.Show("El archivo plano tiene los siguientes errores.", "SOLIDO",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                System.Diagnostics.Process.Start(ruta);
            }
            return ok;
        }

        // ─── GrabaMovimiento wrapper ───────────────────────────────────────────────
        // VB call: clscartera.GrabaMovimiento(Comprobante, ConseCpte, Cedula, lincred,
        //   NumCta, Format(FechaMovto,"yyyyMM"), CptoAho, FechaMovto, 0, valor,
        //   "ACREDITAMOS...", usuario, myconnect, , , nit, , Cedula, , , False)
        // Required mandatory conversions: lincred(str→int), NumCta(str→double),
        //   Periodo(str→int), credito(str→double,ByRef)
        private void GrabaMovimientoHelper(string Comprobante, double ConseCpte,
            string Cedula, int lincred, double NumCta, int Periodo,
            string CptoAho, DateTime FechaMovto, ref double credito,
            string nit, OdbcConnection myconnect)
        {
            // Optional defaults for p14-p47
            int    _ciclo        = 999999;
            string _cuenta       = " ";
            // nit = p16 (caller provides)
            string _factura      = "";
            // Cedula = p18 (IdBenef — caller provides)
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto _clades       = ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Todo;
            int    _numExtra     = 0;
            // RecCuotas = p21 → False (different from VB default True)

            string _tipoDocaux   = "";
            string _numDocAux    = "";
            string _detallefact  = "";
            DateTime _fecVenFact = new DateTime(1950, 1, 1);
            string _banco        = "9999";
            string _numcheque    = " ";
            string _pagoCodeuda  = "99999999999999";
            int    _cicloNomina  = 999999;
            string _cencosto     = "99999999";
            string _usuarioSG    = " ";
            double _vlrSG        = 0;
            string _empDsto      = "9999";
            bool   _desdeApli    = false;
            double _vlrBase      = 0;
            string _esMov        = "CC";
            bool   _desdeGrab    = false;
            double _idSolCred    = 0;
            string _tipoNom      = "";
            DateTime _fecCausac  = new DateTime(1900, 1, 1);
            double _idsolaux     = 0;
            string _aplicaExt    = "Y";
            double _secuencia    = 0;
            bool   _validaSaldo  = true;
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar _prioridad = ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.PrioridadesAtomar.Todos;
            string _trasladoCpto = " ";
            int    _lineaAhorro  = 9999;

            clscartera.GrabaMovimiento(
                Comprobante, ConseCpte, Cedula, lincred, NumCta,
                Periodo, CptoAho, FechaMovto, 0.0, ref credito,
                "ACREDITAMOS VALORES RECIBIDOS DE NOMINA", "", myconnect,
                _ciclo, _cuenta, nit, _factura, Cedula,
                _clades, _numExtra, false,
                _tipoDocaux, _numDocAux, _detallefact, _fecVenFact, _banco, _numcheque,
                _pagoCodeuda, _cicloNomina, _cencosto, _usuarioSG, _vlrSG, _empDsto,
                _desdeApli, _vlrBase, _esMov, _desdeGrab, _idSolCred, _tipoNom, _fecCausac,
                _idsolaux, _aplicaExt, _secuencia, _validaSaldo, _prioridad, _trasladoCpto, _lineaAhorro);
        }

        // ─── BuscarCompania wrapper ────────────────────────────────────────────────
        // VB call: msgconfsys.BuscarCompania(CodCompania, myconnect, , CptoCap,
        //   , , , , , , , , , , , , , , , CptoAho)
        // Extracts: p4=CptoCap, p20=CptoAho
        private void BuscarCompaniaHelper(string CodCompania, OdbcConnection myconnect,
            ref string CptoCap, ref string CptoAho)
        {
            string _u3  = " ";
            // CptoCap = p4 (caller)
            string _u5  = "N";    string _u6  = "0";
            int    _u7  = 0;      decimal _u8  = 0m;   int    _u9  = 0;   decimal _u10 = 0m;
            int    _u11 = 0;      int    _u12 = 0;      int    _u13 = 0;   string _u14 = "00";
            string _u15 = "0";    string _u16 = " ";    string _u17 = " ";
            string _u18 = "9999"; string _u19 = "9999";
            // CptoAho = p20 (caller)
            string _u21 = "9999"; string _u22 = "9999"; string _u23 = "";   string _u24 = "";
            string _u25 = "9999"; string _u26 = " ";    string _u27 = "0";
            string _u28 = "9999"; string _u29 = "99";   string _u30 = "9999";
            double _u31 = 0;      double _u32 = 0;      int    _u33 = 0;   string _u34 = "9999";
            string _u35 = " ";    string _u36 = " ";    double _u37 = 0;
            string _u38 = " ";    string _u39 = "";     string _u40 = "9999";
            string _u41 = "9999"; string _u42 = "9999"; double _u43 = 0;   double _u44 = 0;
            string _u45 = "N";    string _u46 = "N";    string _u47 = "0"; string _u48 = "0";
            string _u49 = "";     string _u50 = "";     string _u51 = "";  string _u52 = "";
            string _u53 = " ";    string _u54 = " ";    string _u55 = " ";
            string _u56 = "9999"; string _u57 = "9999";
            char   _u58 = 'N';    double _u59 = 0;      double _u60 = 0;
            string _u61 = "999999999999"; string _u62 = " "; string _u63 = "   ";
            string _u64 = " ";    char   _u65 = 'N';    int    _u66 = 0;
            string _u67 = "N";    string _u68 = "0";    string _u69 = "Y"; string _u70 = "Y";
            string _u71 = "N";    string _u72 = "1";    int    _u73 = 0;   string _u74 = "EST";

            // msgconfsys.BuscarCompania(CodCompania, myconnect, // ERROR: CS1503
                // ref _u3,  ref CptoCap, ref _u5,  ref _u6, // ERROR: CS1503
                // ref _u7,  ref _u8,    ref _u9,  ref _u10, // ERROR: CS1503
                // ref _u11, ref _u12,   ref _u13, ref _u14, // ERROR: CS1503
                // ref _u15, ref _u16,   ref _u17, // ERROR: CS1503
                // ref _u18, ref _u19,   ref CptoAho, // ERROR: CS1503
                // ref _u21, ref _u22,   ref _u23, ref _u24, // ERROR: CS1503
                // ref _u25, ref _u26,   ref _u27, // ERROR: CS1503
                // ref _u28, ref _u29,   ref _u30, // ERROR: CS1503
                // ref _u31, ref _u32,   ref _u33, ref _u34, // ERROR: CS1503
                // ref _u35, ref _u36,   ref _u37, // ERROR: CS1503
                // ref _u38, ref _u39,   ref _u40, // ERROR: CS1503
                // ref _u41, ref _u42,   ref _u43, ref _u44, // ERROR: CS1503
                // ref _u45, ref _u46,   ref _u47, ref _u48, // ERROR: CS1503
                // ref _u49, ref _u50,   ref _u51, ref _u52, ref _u53, // ERROR: CS1503
                // ref _u54, ref _u55,   ref _u56, ref _u57, // ERROR: CS1503
                // ref _u58, ref _u59,   ref _u60, // ERROR: CS1503
                // ref _u61, ref _u62,   ref _u63, // ERROR: CS1503
                // ref _u64, ref _u65,   ref _u66, // ERROR: CS1503
                // ref _u67, ref _u68,   ref _u69, ref _u70, // ERROR: CS1503
                // ref _u71, ref _u72,   ref _u73, ref _u74); // ERROR: CS1503
        }

        // ─── BuscaAsociado wrapper ─────────────────────────────────────────────────
        // VB call: clscartera.BuscaAsociado(Cedula, myconnect, , nit, , , , , , , , NumCta)
        // Extracts: p4=nit, p12=NumCta (Cuentabanco)
        private bool BuscaAsociadoHelper(ref string Cedula, OdbcConnection myconnect,
            ref string nit, ref string NumCta)
        {
            string _nombre   = " ";
            // nit = p4 (caller)
            string _agencia  = "9999";
            string _telefono = " ";  string _direccion = " ";
            string _email    = " ";  string _celular   = " ";
            int    _natjur   = 0;    string _banco     = "9999";
            // NumCta = p12 (caller)
            string _password = " ";  string _consulEn  = " ";
            string _estatusCon = " "; string _periodd  = " ";
            string _empresa  = "9999"; string _clase   = "5"; string _estado = "R";

            return clscartera.BuscaAsociado(ref Cedula, myconnect,
                ref _nombre, ref nit,
                ref _agencia, ref _telefono, ref _direccion,
                ref _email, ref _celular, ref _natjur, ref _banco,
                ref NumCta,
                ref _password, ref _consulEn, ref _estatusCon,
                ref _periodd, ref _empresa, ref _clase, ref _estado);
        }
    }
}
