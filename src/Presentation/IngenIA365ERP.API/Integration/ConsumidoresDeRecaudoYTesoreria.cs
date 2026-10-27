namespace IngenIA365ERP.API.Integration;

/// <summary>
/// Los consumidores reales de los destinos <c>Treasury</c> y <c>Receipts</c> (feature 015, T0141, T0176): <c>DestinoTesoreria</c> desde US10 y
/// <c>DestinoRecaudo</c> desde US5. Se registran <b>siempre</b>, en todo ambiente: desde la fase 19 (T0307, 2026-10-26) no hay dobles de prueba
/// (<c>RecaudadorDePrueba</c>, <c>TesoreriaDePrueba</c>) ni la bandera transitoria <c>Lending:TestDoubles:Enabled</c> que los alternaba con éstos
/// (FR-223; <c>NoHayDoblesDePrueba</c>, <c>UnSoloConsumidorPorDestino</c>). Lo llama <c>Program.cs</c> una vez. (nuevo)
/// </summary>
public static class ConsumidoresDeRecaudoYTesoreria
{
    public static void Registrar(IServiceCollection services)
    {
        services.AddScoped<IngenIA365ERP.Application.Common.Integration.IDestinoDeMensajes, IngenIA365ERP.Application.Treasury.Integration.DestinoTesoreria>();
        services.AddScoped<IngenIA365ERP.Application.Common.Integration.IDestinoDeMensajes, IngenIA365ERP.Application.Receipts.Integration.DestinoRecaudo>();
    }
}
