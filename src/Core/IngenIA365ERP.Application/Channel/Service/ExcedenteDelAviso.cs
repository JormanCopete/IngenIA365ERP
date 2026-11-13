using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Application.Channel.Service;

/// <summary>
/// Feature 021, US5 (T101; research R6; FR-013, FR-046): arma las partes del aviso antes de aplicarlo, porque un pago aprobado en la pasarela
/// ya es dinero recibido y nunca se rechaza. Con la simulación de cada contrato:
/// <list type="bullet">
/// <item>un concepto que ya no admite el pago (obligación cancelada, cuenta cerrada) se retira y su valor queda a favor del pagador;</item>
/// <item>en un crédito vivo pagado con «exigible» o «parcial», lo que sobra va como <see cref="ModalidadDePago.Advance"/> (anticipo a las
/// próximas cuotas) si la política de la cooperativa es <see cref="SurplusPolicy.AdvanceElseCreditBalance"/>;</item>
/// <item>lo demás que sobre lo deja como saldo a favor <see cref="AplicacionNoInteractiva"/> (el total de la línea menos lo aplicado).</item>
/// </list>
/// No escribe nada: sólo simula. Cada retiro o anticipo deja una nota para el motivo del aviso. (nuevo)
/// </summary>
public static class ExcedenteDelAviso
{
    /// <summary>Un ítem del aviso ya resuelto: el producto, su modalidad en el contrato y el valor.</summary>
    public sealed record ItemResuelto(
        bool EsCredito,
        Guid Producto,
        string ModalidadDelCanal,
        decimal Valor,
        ModalidadDePago? Credito,
        ModalidadDeConsignacion? Cuenta,
        EleccionDeAbono? Eleccion);

    /// <summary>Las partes que se aplican, las notas de lo que no y si algo queda a favor del asociado.</summary>
    public sealed record Reparto(IReadOnlyList<ParteNoInteractiva> Partes, IReadOnlyList<string> Notas, bool ConExcedente);

    public static async Task<Reparto> RepartirAsync(
        IReadOnlyList<ItemResuelto> items,
        Guid titular,
        DateOnly fecha,
        ClaseDeMedioDeRecaudo medio,
        SurplusPolicy politica,
        IContratoDeObligaciones obligaciones,
        IContratoDeCuentas cuentas,
        CancellationToken ct)
    {
        var partes = new List<ParteNoInteractiva>();
        var notas = new List<string>();
        var excedente = false;
        foreach (var i in items)
        {
            if (i.EsCredito)
            {
                var modalidad = i.Credito ?? ModalidadDePago.FreeAmount;
                var sim = await obligaciones.SimularAsync(new SolicitudDeSimulacion(ClavesDeRecaudo.Recaudador, i.Producto, modalidad, i.Valor, fecha, i.Eleccion), ct);
                if (sim.Result == ResultadoDelContrato.Rejected || sim.Value is null)
                {
                    if (AplicacionNoInteractiva.CodigosDeFechaValor.Contains(sim.Code))
                    {
                        // La fecha la resuelve la aplicación (la más antigua que admita el contrato).
                        partes.Add(Parte(ApplicationTarget.Loan, i, titular, i.Valor, modalidad, null));
                        continue;
                    }
                    notas.Add($"El crédito no admitió el pago ({sim.Message}); el valor quedó a favor del asociado.");
                    excedente = true;
                    continue;
                }
                var sobra = Math.Max(0m, sim.Value.Surplus);
                if (sobra >= i.Valor)
                {
                    notas.Add("El crédito ya no tenía qué pagar con esa modalidad; el valor quedó a favor del asociado.");
                    excedente = true;
                    continue;
                }
                partes.Add(Parte(ApplicationTarget.Loan, i, titular, i.Valor - sobra, modalidad, null));
                if (sobra <= 0m) continue;
                var anticipo = politica == SurplusPolicy.AdvanceElseCreditBalance
                    && i.ModalidadDelCanal is ModosDelCanal.Due or ModosDelCanal.Partial;
                if (anticipo)
                {
                    var adelanto = await obligaciones.SimularAsync(new SolicitudDeSimulacion(ClavesDeRecaudo.Recaudador, i.Producto, ModalidadDePago.Advance, sobra,
                        fecha), ct);
                    var noCabe = adelanto.Value is { } a ? Math.Max(0m, a.Surplus) : sobra;
                    if (adelanto.Result != ResultadoDelContrato.Rejected && noCabe < sobra)
                    {
                        partes.Add(Parte(ApplicationTarget.Loan, i, titular, sobra - noCabe, ModalidadDePago.Advance, null));
                        notas.Add(noCabe > 0m
                            ? $"Sobraron {sobra:N0} pesos: {sobra - noCabe:N0} van como anticipo a las próximas cuotas y {noCabe:N0} quedaron a favor del asociado."
                            : $"Sobraron {sobra:N0} pesos: van como anticipo a las próximas cuotas.");
                        excedente |= noCabe > 0m;
                        continue;
                    }
                }
                notas.Add($"Sobraron {sobra:N0} pesos del pago al crédito: quedaron a favor del asociado.");
                excedente = true;
            }
            else
            {
                var modalidad = i.Cuenta ?? ModalidadDeConsignacion.Free;
                var sim = await cuentas.SimularAsync(new SolicitudDeSimulacionDeCuenta(ClavesDeRecaudo.Recaudador, "Deposit", i.Producto, i.Valor, fecha, medio, [], null,
                    modalidad), ct);
                if ((sim.Result == ResultadoDelContrato.Rejected || sim.Value is null || !sim.Value.Allowed)
                    && !AplicacionNoInteractiva.CodigosDeFechaValor.Contains(sim.Code))
                {
                    notas.Add($"La cuenta no admitió la consignación ({sim.Message}); el valor quedó a favor del asociado.");
                    excedente = true;
                    continue;
                }
                partes.Add(Parte(ApplicationTarget.Account, i, titular, i.Valor, null, modalidad));
            }
        }
        return new Reparto(partes, notas, excedente);
    }

    private static ParteNoInteractiva Parte(ApplicationTarget destino, ItemResuelto i, Guid titular, decimal valor, ModalidadDePago? credito,
        ModalidadDeConsignacion? cuenta) =>
        new(destino, i.Producto, titular, valor, LoanModality: credito, AccountModality: cuenta,
            LoanChoice: credito == ModalidadDePago.ExtraordinaryPrincipal ? i.Eleccion : null);
}
