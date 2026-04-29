namespace IngenIA365ERP.Shared.Configuration
{
    public static class AppSettings
    {
        // Configuracion para habilitar/deshabilitar el modo mock
        public static bool UseMockServices { get; set; } = false;

        // Clave de licencia SyncFusion
        public static string SyncFusionLicenseKey { get; set; } = "";

        // URLs de los servicios
        public static string ApiBaseUrl { get; set; } = "http://localhost:5100";
        public static string LocalApiBaseUrl { get; set; } = "http://localhost:5100";

        // Obtener la URL base segun la configuracion
        public static string GetApiBaseUrl()
        {
            return UseMockServices ? LocalApiBaseUrl : ApiBaseUrl;
        }

        // Endpoints
        public static class Endpoints
        {
            public static string Login => $"{GetApiBaseUrl()}/api/auth/login";
            public static string Logout => $"{GetApiBaseUrl()}/api/auth/logout";
        }
    }
}
