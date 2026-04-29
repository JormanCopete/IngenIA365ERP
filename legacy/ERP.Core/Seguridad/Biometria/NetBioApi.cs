#if NITGEN_SDK
namespace ERP.Core.Seguridad.Biometria
{
    /// <summary>
    /// Constantes para NBioAPI
    /// Copyright: NITGEN Co., Ltd.
    /// </summary>
    public static class NetBioApi
    {
        #region Error code

        public const int NBioAPIERROR_NONE = 0;

        #endregion

        #region General

        // True / False
        public const int NBioAPI_TRUE = 1;
        public const int NBioAPI_FALSE = 0;

        #endregion

        #region Device

        // Constant for DeviceID
        public const int NBioAPI_DEVICE_ID_NONE = 0;
        public const int NBioAPI_DEVICE_ID_FDP02_0 = 1;
        public const int NBioAPI_DEVICE_ID_FDU01_0 = 2;
        public const int NBioAPI_DEVICE_ID_OSU02_0 = 3;
        public const int NBioAPI_DEVICE_ID_FDU11_0 = 4;
        public const int NBioAPI_DEVICE_ID_FSC01_0 = 5;
        public const int NBioAPI_DEVICE_ID_FDU03_0 = 6;
        public const int NBioAPI_DEVICE_ID_AUTO_DETECT = 255;

        // Constant for Device Name
        public const int NBioAPI_DEVICE_NAME_FDP02 = 1;
        public const int NBioAPI_DEVICE_NAME_FDU01 = 2;
        public const int NBioAPI_DEVICE_NAME_OSU02 = 3;
        public const int NBioAPI_DEVICE_NAME_FDU11 = 4;
        public const int NBioAPI_DEVICE_NAME_FSC01 = 5;
        public const int NBioAPI_DEVICE_NAME_FDU03 = 6;

        #endregion

        #region BSP

        // Constant for Security Level
        public const int NBioAPI_FIR_SECURITY_LEVEL_LOWEST = 1;
        public const int NBioAPI_FIR_SECURITY_LEVEL_LOWER = 2;
        public const int NBioAPI_FIR_SECURITY_LEVEL_LOW = 3;
        public const int NBioAPI_FIR_SECURITY_LEVEL_BELOW_NORMAL = 4;
        public const int NBioAPI_FIR_SECURITY_LEVEL_NORMAL = 5;
        public const int NBioAPI_FIR_SECURITY_LEVEL_ABOVE_NORMAL = 6;
        public const int NBioAPI_FIR_SECURITY_LEVEL_HIGH = 7;
        public const int NBioAPI_FIR_SECURITY_LEVEL_HIGHER = 8;
        public const int NBioAPI_FIR_SECURITY_LEVEL_HIGHEST = 9;

        // Purpose for FIR
        public const int NBioAPI_FIR_PURPOSE_VERIFY = 1;
        public const int NBioAPI_FIR_PURPOSE_IDENTIFY = 2;
        public const int NBioAPI_FIR_PURPOSE_ENROLL = 3;
        public const int NBioAPI_FIR_PURPOSE_ENROLL_FOR_VERIFICATION_ONLY = 4;
        public const int NBioAPI_FIR_PURPOSE_ENROLL_FOR_IDENTIFICATION_ONLY = 5;
        public const int NBioAPI_FIR_PURPOSE_AUDIT = 6;
        public const int NBioAPI_FIR_PURPOSE_UPDATE = 10;

        // Finger ID
        public const int NBioAPI_FINGER_ID_UNKNOWN = 0;
        public const int NBioAPI_FINGER_ID_RIGHT_THUMB = 1;
        public const int NBioAPI_FINGER_ID_RIGHT_INDEX = 2;
        public const int NBioAPI_FINGER_ID_RIGHT_MIDDLE = 3;
        public const int NBioAPI_FINGER_ID_RIGHT_RING = 4;
        public const int NBioAPI_FINGER_ID_RIGHT_LITTLE = 5;
        public const int NBioAPI_FINGER_ID_LEFT_THUMB = 6;
        public const int NBioAPI_FINGER_ID_LEFT_INDEX = 7;
        public const int NBioAPI_FINGER_ID_LEFT_MIDDLE = 8;
        public const int NBioAPI_FINGER_ID_LEFT_RING = 9;
        public const int NBioAPI_FINGER_ID_LEFT_LITTLE = 10;

        // Window Style
        public const int NBioAPI_WINDOW_STYLE_POPUP = 0;
        public const int NBioAPI_WINDOW_STYLE_INVISIBLE = 1;     // only for NBioAPI_Capture()
        public const int NBioAPI_WINDOW_STYLE_CONTINUOUS = 2;
        public const int NBioAPI_WINDOW_STYLE_NO_FPIMG = 65536;
        public const int NBioAPI_WINDOW_STYLE_TOPMOST = 131072;  // currently not used (after v2.3)
        public const int NBioAPI_WINDOW_STYLE_NO_WELCOME = 262144;
        public const int NBioAPI_WINDOW_STYLE_NO_TOPMOST = 524288;

        #endregion

        #region Export Data

        public const int MINCONV_TYPE_FDP = 0;
        public const int MINCONV_TYPE_FDU = 1;
        public const int MINCONV_TYPE_FDA = 2;
        public const int MINCONV_TYPE_OLD_FDA = 3;
        public const int MINCONV_TYPE_FDAC = 4;
        public const int MINCONV_TYPE_FIM10_HV = 5;
        public const int MINCONV_TYPE_FIM10_LV = 6;
        public const int MINCONV_TYPE_FIM01_HV = 7;
        public const int MINCONV_TYPE_FIM01_HD = 8;
        public const int MINCONV_TYPE_FELICA = 9;
        public const int MINCONV_TYPE_EXTENSION = 10;
        public const int MINCONV_TYPE_TEMPLATESIZE_32 = 11;
        public const int MINCONV_TYPE_TEMPLATESIZE_48 = 12;
        public const int MINCONV_TYPE_TEMPLATESIZE_64 = 13;
        public const int MINCONV_TYPE_TEMPLATESIZE_80 = 14;
        public const int MINCONV_TYPE_TEMPLATESIZE_96 = 15;
        public const int MINCONV_TYPE_TEMPLATESIZE_112 = 16;
        public const int MINCONV_TYPE_TEMPLATESIZE_128 = 17;
        public const int MINCONV_TYPE_TEMPLATESIZE_144 = 18;
        public const int MINCONV_TYPE_TEMPLATESIZE_160 = 19;
        public const int MINCONV_TYPE_TEMPLATESIZE_176 = 20;
        public const int MINCONV_TYPE_TEMPLATESIZE_192 = 21;
        public const int MINCONV_TYPE_TEMPLATESIZE_208 = 22;
        public const int MINCONV_TYPE_TEMPLATESIZE_224 = 23;
        public const int MINCONV_TYPE_TEMPLATESIZE_240 = 24;
        public const int MINCONV_TYPE_TEMPLATESIZE_256 = 25;
        public const int MINCONV_TYPE_TEMPLATESIZE_272 = 26;
        public const int MINCONV_TYPE_TEMPLATESIZE_288 = 27;
        public const int MINCONV_TYPE_TEMPLATESIZE_304 = 28;
        public const int MINCONV_TYPE_TEMPLATESIZE_320 = 29;
        public const int MINCONV_TYPE_TEMPLATESIZE_336 = 30;
        public const int MINCONV_TYPE_TEMPLATESIZE_352 = 31;
        public const int MINCONV_TYPE_TEMPLATESIZE_368 = 32;
        public const int MINCONV_TYPE_TEMPLATESIZE_384 = 33;
        public const int MINCONV_TYPE_TEMPLATESIZE_400 = 34;

        #endregion

        #region Export Image

        // Constant for FP Image
        public const int NBioAPI_IMG_TYPE_RAW = 1;
        public const int NBioAPI_IMG_TYPE_BMP = 2;
        public const int NBioAPI_IMG_TYPE_JPG = 3;

        #endregion
    }
}
#endif
