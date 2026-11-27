using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payoffs;

/// <summary>
/// La cuota causada de un crédito recogido a la fecha proyectada: su número, su valor pendiente y su vencimiento (feature 028, FR-003; el vencimiento,
/// desde T010: no se guarda, sale del plan). (nuevo)
/// </summary>
public sealed record CuotaCausadaDeRecogida(int Numero, decimal Valor, DateOnly? Vencimiento = null);

/// <summary>
/// Lo que la liquidación de una recogida dice a la fecha proyectada D con la información hasta R (feature 028, R3), como lo guarda la solicitud
/// (<c>LND_CreditApplicationPayoffs</c>): el desglose, la huella y, en un crédito, el modo de intereses resuelto, el tratamiento de la cuota causada y
/// la cuota con su envío. (nuevo)
/// </summary>
/// <param name="FechaProyectada">D.</param>
/// <param name="InformacionHasta">R.</param>
/// <param name="Desglose">Lo que se recogería, por concepto; su total es el valor proyectado.</param>
/// <param name="Huella">La huella de la liquidación (<c>sha256:</c>…).</param>
/// <param name="ModoDeIntereses">Sólo créditos: <c>Corrido</c> o <c>CuotaCompleta</c>, resuelto a D.</param>
/// <param name="TratamientoDeCuota">Sólo créditos: incluir o excluir la cuota causada, ya resuelto.</param>
/// <param name="CuotaCausada">Sólo créditos: la cuota causada a D.</param>
/// <param name="EstadoDeEnvio">Sólo créditos: en qué va la cuota causada frente a su canal.</param>
/// <param name="ArchivoDeLibranzaId">El archivo de libranza en que va la cuota causada, si está enviada a una pagaduría.</param>
public sealed record ProyeccionDeRecogida(
    DateOnly FechaProyectada,
    DateTime InformacionHasta,
    DesgloseDeRecogida Desglose,
    string Huella,
    string? ModoDeIntereses = null,
    InstallmentTreatment? TratamientoDeCuota = null,
    CuotaCausadaDeRecogida? CuotaCausada = null,
    SentInstallmentState EstadoDeEnvio = SentInstallmentState.None,
    int? ArchivoDeLibranzaId = null);
