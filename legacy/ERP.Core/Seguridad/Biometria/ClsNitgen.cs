#if NITGEN_SDK
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
#if NITGEN_SDK
using NITGEN.SDK.NBioBSP;
#endif

namespace ERP.Core.Seguridad.Biometria
{
    /// <summary>
    /// Clase para manejo de lectores de huella digital Nitgen
    /// </summary>
    public class ClsNitgen
    {
        #region Campos privados

        private OdbcCommand _mycomqueryconec = new OdbcCommand();
        private NBioAPI _mNBioAPI = new NBioAPI();
        private NBioAPI.Type.WINDOW_OPTION _mWinOption = new NBioAPI.Type.WINDOW_OPTION();
        private NBioAPI.Type.HFIR _mhNewFIR = new NBioAPI.Type.HFIR();
        private NBioAPI.Type.FIR _mbiFIR = new NBioAPI.Type.FIR();
        private NBioAPI.Type.FIR_TEXTENCODE _mTextFIR = new NBioAPI.Type.FIR_TEXTENCODE();
        private NBioAPI.Type.FIR_TEXTENCODE _mTextFIR1 = new NBioAPI.Type.FIR_TEXTENCODE();
        private NBioAPI.Type.DEVICE_INFO_EX _mDeviceInfoEx = new NBioAPI.Type.DEVICE_INFO_EX();

        private NBioAPI.Export.EXPORT_DATA _image;
        private short _mOpenedDeviceID;
        private string _szTextEncodeFIR;
        private bool _ok;
        private byte[] _biFIR;
        private ERP.Core.Compartido.Datos.ClsConect _connect = new ERP.Core.Compartido.Datos.ClsConect();

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor de la clase ClsNitgen
        /// </summary>
        public ClsNitgen()
        {
            _mWinOption.Option2 = new NBioAPI.Type.WINDOW_OPTION_2();
        }

        #endregion

        #region Metodos de Enrolamiento

        /// <summary>
        /// Realiza el enrolamiento de una huella digital
        /// </summary>
        /// <param name="stCodigo">Codigo del usuario</param>
        /// <param name="stText">Texto de la huella codificada (salida)</param>
        /// <param name="stTextByte">Bytes de la huella (salida)</param>
        /// <returns>True si el enrolamiento fue exitoso</returns>
        public bool Enrrolamiento(string stCodigo, ref string stText, ref byte[] stTextByte)
        {
            NBioAPI.Type.FIR_PAYLOAD myPayload = new NBioAPI.Type.FIR_PAYLOAD();
            short iDeviceID = NBioAPI.Type.DEVICE_ID.AUTO;
            uint ret;

            iDeviceID = NBioAPI.Type.DEVICE_ID.AUTO;
            myPayload.Data = stCodigo;

            _mNBioAPI.CloseDevice(_mOpenedDeviceID);
            ret = _mNBioAPI.OpenDevice(iDeviceID);

            if (ret == NBioAPI.Error.NONE)
            {
                _mOpenedDeviceID = iDeviceID;
            }
            else
            {
                MessageBox.Show("Error abriendo el device", "Nitgen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            ret = _mNBioAPI.Enroll(out _mhNewFIR, myPayload);

            if (ret == NBioAPI.Error.NONE)
            {
                _mNBioAPI.GetFIRFromHandle(_mhNewFIR, out _mbiFIR);
                _mNBioAPI.GetTextFIRFromHandle(_mhNewFIR, out _mTextFIR, true);
                stText = _mTextFIR.TextFIR;
                stTextByte = _mbiFIR.Data;
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Metodos de Busqueda

        /// <summary>
        /// Busca las huellas registradas en la base de datos
        /// </summary>
        private void BuscaHuella(OdbcConnection myconnect, ref DataSet dsDataset)
        {
            DataSet dsDattaset = new DataSet();
            System.Text.StringBuilder stBuilder = new System.Text.StringBuilder();

            try
            {
                dsDataset?.Tables.Remove("tblhuella");
            }
            catch (Exception)
            {
                // Ignorar si la tabla no existe
            }

            stBuilder.Append("Select codigoter, sthuella from cop_huellafirma ");

            _connect.ExecuteQueryDataset(stBuilder.ToString(), myconnect, "BuscaHuella", ref dsDattaset, "tblhuella");

            if (dsDataset != null)
            {
                dsDataset = dsDattaset;
            }
        }

        /// <summary>
        /// Ejecuta una consulta SQL y llena un DataSet
        /// </summary>
        public bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string nombreProcedimiento, ref DataSet dsDataset, string nombreTabla)
        {
            OdbcDataAdapter myread = new OdbcDataAdapter();
            stMysql = stMysql.Replace("''", "' '");
            _mycomqueryconec.CommandText = stMysql;
            _mycomqueryconec.Connection = appadoConect;
            _mycomqueryconec.ExecuteNonQuery();

            myread.SelectCommand = _mycomqueryconec;
            myread.Fill(dsDataset, nombreTabla);

            return dsDataset.Tables[nombreTabla].Rows.Count > 0;
        }

        /// <summary>
        /// Revisa si una huella existe en la base de datos
        /// </summary>
        /// <param name="myconnect">Conexion a la base de datos</param>
        /// <param name="stHuella">Huella a verificar</param>
        /// <returns>Codigo del tercero si la huella coincide, espacio en blanco si no</returns>
        public string RevisaHuella(OdbcConnection myconnect, string stHuella)
        {
            DataSet dsDataSet = new DataSet();
            double fila = 0;
            string stTexto;

            BuscaHuella(myconnect, ref dsDataSet);

            while (fila <= dsDataSet.Tables["tblhuella"].Rows.Count - 1)
            {
                DataRow row = dsDataSet.Tables["tblhuella"].Rows[(int)fila];
                stTexto = row["sthuella"].ToString();

                // Reemplazar la línea que usa string.IsNullOrWhiteSpace por una alternativa compatible con versiones antiguas de .NET
                if (stTexto == null || stTexto.Trim().Length == 0)
                {
                    _ok = false;
                }
                else
                {
                    _ok = VerificaTexto(stTexto, stHuella);
                }

                if (_ok)
                {
                    return row["codigoter"].ToString();
                }

                fila += 1;
            }

            return " ";
        }

        #endregion

        #region Metodos de Verificacion

        /// <summary>
        /// Verifica si dos huellas codificadas en texto coinciden
        /// </summary>
        /// <param name="stTexto">Huella almacenada</param>
        /// <param name="stTextoOriginal">Huella a comparar</param>
        /// <returns>True si las huellas coinciden</returns>
        public bool VerificaTexto(string stTexto, string stTextoOriginal)
        {
            uint ret;
            bool result;
            NBioAPI.Type.FIR_PAYLOAD myPayload = new NBioAPI.Type.FIR_PAYLOAD();

            _mTextFIR.TextFIR = stTexto;
            _mTextFIR1.TextFIR = stTextoOriginal;

            ret = _mNBioAPI.VerifyMatch(_mTextFIR1, _mTextFIR, out result, myPayload);

            if (ret != NBioAPI.Error.NONE)
            {
                MessageBox.Show("Huella es incorrecta", "Nitgen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (result)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica una huella en formato binario
        /// </summary>
        /// <param name="stTexto">Huella en bytes</param>
        /// <returns>True si la huella es correcta</returns>
        public bool VerificaBinary(byte[] stTexto)
        {
            uint ret;
            bool result;
            NBioAPI.Type.FIR_PAYLOAD myPayload = new NBioAPI.Type.FIR_PAYLOAD();

            _mbiFIR.Data = stTexto;
            ret = _mNBioAPI.Verify(_mbiFIR, out result, myPayload);

            if (ret != NBioAPI.Error.NONE)
            {
                MessageBox.Show("Huella es incorrecta", "Nitgen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (result)
            {
                return true;
            }
            else
            {
                MessageBox.Show("Huella es incorrecta", "Nitgen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        #endregion

        #region Metodos de Captura

        /// <summary>
        /// Captura una huella digital con visualizacion en PictureBox
        /// </summary>
        /// <param name="picture">PictureBox donde mostrar la huella</param>
        /// <param name="visible">Si es true muestra ventana popup, si es false captura invisible</param>
        /// <returns>Huella codificada en texto</returns>
        public virtual string Captura(ref PictureBox picture, bool visible = false)
        {
            NBioAPI.Type.HFIR hCapturedFIR = new NBioAPI.Type.HFIR();

            SetInitValue(ref picture, visible);
            _mNBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            _mNBioAPI.Capture(out hCapturedFIR, NBioAPI.Type.TIMEOUT.DEFAULT, _mWinOption);
            _mNBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            _mNBioAPI.GetTextFIRFromHandle(hCapturedFIR, out _mTextFIR, true);

            return _mTextFIR.TextFIR;
        }

        /// <summary>
        /// Captura una huella digital sin visualizacion
        /// </summary>
        /// <param name="visible">Si es true muestra ventana popup</param>
        /// <returns>Huella codificada en texto</returns>
        public virtual string Captura(bool visible = false)
        {
            NBioAPI.Type.HFIR hCapturedFIR = new NBioAPI.Type.HFIR();
            PictureBox pictureBox = new PictureBox();

            SetInitValue(ref pictureBox, visible);
            _mNBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            _mNBioAPI.Capture(out hCapturedFIR, NBioAPI.Type.TIMEOUT.DEFAULT, _mWinOption);
            _mNBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            _mNBioAPI.GetTextFIRFromHandle(hCapturedFIR, out _mTextFIR, true);

            return _mTextFIR.TextFIR;
        }

        /// <summary>
        /// Configura los valores iniciales para la captura
        /// </summary>
        private void SetInitValue(ref PictureBox picture, bool visible)
        {
            string textFpColor = "000000";
            string textBkColor = "FFFFFF";

            if (!visible)
            {
                _mWinOption.WindowStyle = NBioAPI.Type.WINDOW_STYLE.INVISIBLE;
                _mWinOption.Option2.FPForeColor[0] = Convert.ToByte(textFpColor.Substring(0, 2), 16);
                _mWinOption.Option2.FPForeColor[1] = Convert.ToByte(textFpColor.Substring(2, 2), 16);
                _mWinOption.Option2.FPForeColor[2] = Convert.ToByte(textFpColor.Substring(4, 2), 16);
                _mWinOption.Option2.FPBackColor[0] = Convert.ToByte(textBkColor.Substring(0, 2), 16);
                _mWinOption.Option2.FPBackColor[1] = Convert.ToByte(textBkColor.Substring(2, 2), 16);
                _mWinOption.Option2.FPBackColor[2] = Convert.ToByte(textBkColor.Substring(4, 2), 16);
                _mWinOption.FingerWnd = (uint)picture.Handle.ToInt32();
            }
            else
            {
                _mWinOption.WindowStyle = NBioAPI.Type.WINDOW_STYLE.POPUP;
            }
        }

        #endregion
    }
}
#endif
