using IngenIA365ERP.Application.Common.GoLive;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>
/// Las condiciones automáticas de Cartera y Aportes en la lista de go/no-go (feature 015, US24, T0320; R-28; FR-227; SC-001): sus requisitos de activación en verde, leídos
/// con las mismas consultas de <c>/cartera/activacion</c> (<see cref="GetLendingActivationStatusQuery"/>) y de la activación de aportes
/// (<see cref="GetDepositsActivationStatusQuery"/> con <c>Contribution</c>; COOFLOPAL sale con créditos y aportes, sin captaciones). Ya activado, una condición verde
/// con la fecha; sin activar, una condición por requisito (<c>CAR.{código}</c>, <c>APO.{código}</c>), donde los de Recaudo y Tesorería reales
/// (<c>ReceiptsCollectorRegistered</c>, <c>TreasuryDestinationRegistered</c>) dicen si la ventanilla ya los liberó. Un requisito «no aplica» cuenta en verde. Sólo lee. (nuevo)
/// </summary>
public sealed class VerificacionDeSalidaDeCartera(ISender sender) : IVerificacionDeSalida
{
    public const string ModuloDeLaLista = "CAR";
    public const string CreditosActivados = "CAR.Activated";
    public const string AportesActivados = "APO.Activated";

    public string Modulo => ModuloDeLaLista;

    public async Task<IReadOnlyList<ResultadoDeVerificacion>> VerificarAsync(CancellationToken ct)
    {
        var lista = new List<ResultadoDeVerificacion>();
        var creditos = await sender.Send(new GetLendingActivationStatusQuery(), ct);
        if (creditos.IsSuccess)
        {
            if (creditos.Value.ActivationDate is { } fecha)
                lista.Add(new(CreditosActivados, "Cartera de créditos activada.", true, $"Activada desde el {fecha:dd/MM/yyyy}."));
            else
                lista.AddRange(creditos.Value.Requirements.Select(r => Condicion("CAR.", "Activación de Cartera", r)));
        }
        else
            lista.Add(new(CreditosActivados, "Cartera de créditos activada.", false, creditos.Error.Message));

        var aportes = await sender.Send(new GetDepositsActivationStatusQuery(LineNature.Contribution), ct);
        if (aportes.IsSuccess)
        {
            if (aportes.Value.ActivationDate is { } fecha)
                lista.Add(new(AportesActivados, "Aportes activados.", true, $"Activados desde el {fecha:dd/MM/yyyy}."));
            else
                lista.AddRange(aportes.Value.Requirements.Select(r => Condicion("APO.", "Activación de aportes", r)));
        }
        else
            lista.Add(new(AportesActivados, "Aportes activados.", false, aportes.Error.Message));
        return lista;
    }

    private static ResultadoDeVerificacion Condicion(string prefijo, string titulo, RequisitoDeActivacionDto r)
    {
        var verde = r.Met || r.Status == EstadosDeRequisito.NotApplicable;
        return new(prefijo + r.Code, $"{titulo}: {r.Code}", verde, r.Detail);
    }
}
