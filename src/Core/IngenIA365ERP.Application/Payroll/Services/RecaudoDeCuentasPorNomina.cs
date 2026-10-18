using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Un descuento de una cuenta que Nómina entrega a la Cartera: la cuenta, el empleado que paga, lo descontado, lo esperado, lo no descontado con su motivo
/// y la fila de <c>PAY_PayrollAccountDeductions</c>. (nuevo)
/// </summary>
public sealed record DescuentoDeCuentaDeNomina(
    Guid AccountPublicId,
    Guid PersonPublicId,
    decimal Descontado,
    decimal NoDescontado,
    MotivoDeNoDescuento Motivo,
    string? Detalle,
    Guid FilaPublicId);

/// <summary>Lo que respondió la Cartera por una cuenta: la consignación (si hubo descuento) y los no descontados de sus cuotas. (nuevo)</summary>
public sealed record RecaudoDeCuentaDeNomina(
    Guid AccountPublicId,
    string Clave,
    RespuestaDelContratoDto<ComprobanteDeCuentaDto>? Consignacion,
    IReadOnlyList<RespuestaDelContratoDto<NoDescontadoDeCuentaRegistradoDto>> NoDescontados)
{
    /// <summary>La Cartera tomó lo descontado (o ya lo había tomado con la misma clave).</summary>
    public bool Aplicado => Consignacion is { Result: ResultadoDelContrato.Applied or ResultadoDelContrato.AlreadyApplied, Value: not null };

    /// <summary>Lo que la Cartera tomó; cero si lo rechazó o si no hubo descuento.</summary>
    public decimal ValorAplicado => Aplicado ? Consignacion!.Value!.Amount : 0m;

    /// <summary>La Cartera lo rechazó porque Contabilidad no tiene cómo registrarlo (A8 = «Bloquear todo»): la aprobación entera falla.</summary>
    public bool NoContabilizable => Consignacion is { Result: ResultadoDelContrato.Rejected } c && c.Code == RecaudoPorNomina.CodigoNoContabilizable;
}

/// <summary>
/// Nómina como recaudador del <b>contrato de cuentas</b> (feature 014, US3, T089; E-02, R18; contrato-de-cuentas §6.1): al aprobar la ordinaria, por cada
/// cuenta, consigna lo descontado como <c>Installments</c> (cubre las cuotas en orden de vencimiento) con <see cref="Recaudador.Nomina"/>, clase de medio
/// <see cref="ClaseDeMedioDeRecaudo.OwnPayrollDeduction"/>, depositante el empleado, recibo el NM, fecha valor el fin del período y clave
/// <c>NOM:{runPublicId:N}:{accountPublicId:N}</c>; lo no descontado lo informa cuota por cuota (la cobertura en orden de vencimiento deja las últimas
/// pendientes) con la misma clave, y la cuota sigue exigible (US3-2). Al reversar la corrida, reversa cada consignación con su clave. Se llama directo
/// dentro de la transacción de la aprobación o de la reversión, como <see cref="RecaudoPorNomina"/>. Un lote vacío no llama a la Cartera. (nuevo)
/// </summary>
public sealed class RecaudoDeCuentasPorNomina(IContratoDeCuentas contrato)
{
    /// <summary>La clave de la consignación de Nómina en una cuenta (contrato-de-cuentas §3.12).</summary>
    public static string Clave(Guid runPublicId, Guid accountPublicId) => $"NOM:{runPublicId:N}:{accountPublicId:N}";

    public async Task<IReadOnlyList<RecaudoDeCuentaDeNomina>> AplicarAsync(Guid runPublicId, ReferenciaDeRecibo recibo, DateOnly fechaValor, DateOnly hoy,
        DateOnly inicio, DateOnly fin, IReadOnlyList<DescuentoDeCuentaDeNomina> descuentos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descuentos);
        if (descuentos.Count == 0) return [];
        var items = descuentos.OrderBy(d => d.AccountPublicId).ToList();

        // Las cuotas de cada cuenta en el orden en que la consignación las cubre: lo no descontado queda en las últimas.
        var conFaltante = items.Where(d => d.NoDescontado > 0m).ToList();
        var cuotasPorCuenta = new Dictionary<Guid, List<ExigibleDeCuentaDto>>();
        if (conFaltante.Count > 0)
        {
            var exigibles = await contrato.ListarExigiblesAsync(new SolicitudDeExigibles(Recaudador.Nomina,
                conFaltante.Select(d => d.PersonPublicId).Distinct().ToList(), hoy, inicio, fin), ct);
            foreach (var g in (exigibles.Value?.Installments ?? []).GroupBy(e => e.AccountPublicId))
                cuotasPorCuenta[g.Key] = g.OrderBy(e => e.DueDate).ThenBy(e => e.Period, StringComparer.Ordinal).ToList();
        }

        var depositos = new List<ConsignacionAAplicar>();
        var noDescontados = new List<NoDescontadoDeCuenta>();
        var reciboCorto = RecaudoPorNomina.Recibo(recibo);
        foreach (var d in items)
        {
            var clave = Clave(runPublicId, d.AccountPublicId);
            if (d.Descontado > 0m)
                depositos.Add(new ConsignacionAAplicar(clave, d.AccountPublicId, ModalidadDeConsignacion.Installments, d.Descontado, fechaValor,
                    ClaseDeMedioDeRecaudo.OwnPayrollDeduction, d.PersonPublicId, reciboCorto));
            if (d.NoDescontado <= 0m || !cuotasPorCuenta.TryGetValue(d.AccountPublicId, out var cuotas)) continue;
            var cubre = d.Descontado;
            foreach (var cuota in cuotas)
            {
                var aplica = Math.Min(cubre, cuota.Pending);
                cubre -= aplica;
                var falta = cuota.Pending - aplica;
                if (falta <= 0m) continue;
                noDescontados.Add(new NoDescontadoDeCuenta(clave, d.AccountPublicId, cuota.InstallmentPublicId, cuota.Pending, falta, d.Motivo, d.Detalle,
                    d.FilaPublicId));
            }
        }
        if (depositos.Count == 0 && noDescontados.Count == 0) return items.Select(d => new RecaudoDeCuentaDeNomina(d.AccountPublicId, Clave(runPublicId, d.AccountPublicId), null, [])).ToList();

        var respuesta = await contrato.ConsignarAsync(new SolicitudDeConsignacion(Recaudador.Nomina, FormaDeAplicacion.NonInteractive, depositos, noDescontados), ct);
        return items.Select(d =>
        {
            var i = depositos.FindIndex(x => x.AccountPublicId == d.AccountPublicId);
            var consignacion = i >= 0 && i < respuesta.Deposits.Count ? respuesta.Deposits[i] : null;
            var registrados = noDescontados.Select((n, j) => (n, j)).Where(x => x.n.AccountPublicId == d.AccountPublicId && x.j < respuesta.NotDeducted.Count)
                .Select(x => respuesta.NotDeducted[x.j]).ToList();
            return new RecaudoDeCuentaDeNomina(d.AccountPublicId, Clave(runPublicId, d.AccountPublicId), consignacion, registrados);
        }).ToList();
    }

    /// <summary>Reversa la consignación que Nómina aplicó en esa cuenta con esa versión de la corrida.</summary>
    public Task<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>> ReversarAsync(Guid runPublicId, Guid accountPublicId, string motivo,
        ReferenciaDeRecibo? recibo, CancellationToken ct) =>
        contrato.ReversarAsync(new SolicitudDeReversoDeCuenta(Recaudador.Nomina, Clave(runPublicId, accountPublicId), motivo,
            recibo is null ? null : RecaudoPorNomina.Recibo(recibo)), ct);
}
