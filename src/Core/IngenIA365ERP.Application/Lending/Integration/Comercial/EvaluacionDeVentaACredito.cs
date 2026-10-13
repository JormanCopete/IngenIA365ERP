using System.Text.Json;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>Una verificación de FR-051 con su resultado y su evidencia (va en <c>ChecksJson</c>). (nuevo)</summary>
public sealed record VerificacionDeVenta(string Check, bool Passed, string? Code = null, string? Reason = null, object? Evidence = null);

/// <summary>
/// El resultado de evaluar una venta a crédito (FR-051, FR-052): cada verificación en orden y la primera que falló (su código y su motivo van al
/// crédito marcado y a la respuesta), y si esa falla es de las que ninguna atribución exceptúa (FR-038). (nuevo)
/// </summary>
public sealed record EvaluacionDeVenta(IReadOnlyList<VerificacionDeVenta> Verificaciones)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Sin evaluar: la venta llegó ya validada en línea (<c>pendingValidation = false</c>).</summary>
    public static EvaluacionDeVenta SinReevaluar { get; } = new([new VerificacionDeVenta("ValidatedOnline", true)]);

    public VerificacionDeVenta? Falla => Verificaciones.FirstOrDefault(v => !v.Passed);

    public bool Fallida => Falla is not null;

    public string ChecksJson => JsonSerializer.Serialize(Verificaciones, Json);

    /// <summary>La evaluación con una verificación más (la reserva que no se encontró, C20).</summary>
    public EvaluacionDeVenta Con(VerificacionDeVenta v) => new([.. Verificaciones, v]);
}

/// <summary>
/// La evaluación de una venta a crédito pendiente de validar (feature 013, US6, T0589; FR-051, FR-052; mensajes.md §15.3 paso 6): con lo vigente
/// <b>a la fecha de la venta</b> y sin contarla, en orden de llegada (el consumidor corre bajo el cerrojo del deudor y lo ya creado cuenta):
/// <list type="number">
/// <item>el estado del deudor (bloqueos vigentes; <c>Lending.Debtor.Blocked</c>);</item>
/// <item>el cupo sin contar la venta ni su reserva (<see cref="DisponibleDeCupo.ParaVentaAsync"/>; un cliente necesita cupo otorgado, FR-049;
/// <c>Lending.CreditLimit.Insufficient</c>);</item>
/// <item>la concentración con el patrimonio técnico vigente (<see cref="ValidacionDeConcentracion"/>; <c>Lending.Concentration.*</c>);</item>
/// <item>la usura del día de la venta (<c>Lending.Usury.Exceeded</c>);</item>
/// <item>la línea con origen comercial y la venta dentro de sus rangos (<c>Lending.Schedule.OutOfLineRange</c>);</item>
/// <item>las listas restrictivas, <b>guardando</b> <c>LND_ScreeningResults</c> (es una operación; una coincidencia bloquea y alerta al oficial con
/// <c>Cartera.CoincidenciaEnListas</c>);</item>
/// <item>la autorización vigente <c>CreditProcessing</c> (<c>Lending.Authorization.Missing</c>).</item>
/// </list>
/// Corre todas y la primera que falla es el motivo. Una aprobación de Comercial no sustituye una atribución de Cartera: no se mira. No guarda. (nuevo)
/// </summary>
public sealed class EvaluacionDeVentaACredito(
    IBloqueosDelDeudor bloqueos,
    DisponibleDeCupo cupos,
    ValidacionDeConcentracion concentracion,
    IConsultaDeListasDeCartera listas,
    IAutorizacionDeDatos autorizaciones)
{
    public const string Estado = "DebtorStatus";
    public const string Cupo = "CreditLimit";
    public const string Concentracion = "Concentration";
    public const string Usura = "Usury";
    public const string LineaComercial = "CommercialLine";
    public const string Listas = "RestrictiveLists";
    public const string Autorizacion = "Authorization";

    /// <summary>Los códigos de falla que ninguna atribución exceptúa (FR-038, <see cref="NoExceptuables"/>).</summary>
    public static IReadOnlyDictionary<string, MotivoNoExceptuable> NoExceptuablesPorCodigo { get; } = new Dictionary<string, MotivoNoExceptuable>
    {
        [ErroresDeOriginacion.CodigoCoincidenciaEnListas] = MotivoNoExceptuable.RestrictiveListMatch,
        [ErroresDeOriginacion.CodigoUsuraExcedida] = MotivoNoExceptuable.Usury,
        [ErroresDeOriginacion.CodigoSinAutorizacion] = MotivoNoExceptuable.HolderAuthorization,
        [ErroresDeOriginacion.CodigoCapacidadDePago] = MotivoNoExceptuable.PaymentCapacity,
    };

    /// <param name="venta">La venta convertida (o, en una nota débito, la obligación con el valor que sube).</param>
    /// <param name="usura">El control de usura del plan de la venta; nulo si no hay plan nuevo que controlar (una nota débito sobre el plan vigente).</param>
    /// <param name="fueraDeRango">Si la venta salió de los rangos de la línea.</param>
    /// <param name="monto">El valor a evaluar; nulo, el de la venta.</param>
    public async Task<EvaluacionDeVenta> EvaluarAsync(VentaConvertida venta, Domain.Lending.Rates.ResultadoDeUsura? usura, bool fueraDeRango, CancellationToken ct,
        decimal? monto = null)
    {
        ArgumentNullException.ThrowIfNull(venta);
        var fecha = venta.Venta.Fecha;
        var persona = venta.Persona;
        var valor = monto ?? venta.Monto;
        var v = new List<VerificacionDeVenta>();

        var vigentes = await bloqueos.VigentesAsync([persona.Id], ct);
        v.Add(vigentes.Count == 0
            ? new VerificacionDeVenta(Estado, true)
            : new VerificacionDeVenta(Estado, false, ErroresDeOriginacion.CodigoDeudorBloqueado,
                ConsultasDeCartera.MotivoDe(vigentes.OrderBy(b => (int)b.Reason).First().Reason),
                new { blocks = vigentes.Select(b => b.Reason.ToString()) }));

        var cupo = await cupos.ParaVentaAsync(persona.Id, venta.Linea.Id, fecha, ct, sinElPago: venta.Venta.Pago.PaymentPublicId);
        if (cupo is null)
            v.Add(venta.TipoDeDeudor == DebtorKind.Customer
                ? new VerificacionDeVenta(Cupo, false, ErroresDeOriginacion.CodigoCupoInsuficiente, "El cliente no tiene cupo otorgado para comprar a crédito.")
                : new VerificacionDeVenta(Cupo, true, Evidence: new { limit = (decimal?)null }));
        else
            v.Add(valor <= cupo.Disponible
                ? new VerificacionDeVenta(Cupo, true, Evidence: new { creditLimit = cupo.Cupo.PublicId, limit = cupo.Limite, available = cupo.Disponible })
                : new VerificacionDeVenta(Cupo, false, ErroresDeOriginacion.CodigoCupoInsuficiente,
                    $"La venta ({ErroresDeOriginacion.Pesos(valor)}) supera el cupo disponible a su fecha ({ErroresDeOriginacion.Pesos(cupo.Disponible)}).",
                    new { creditLimit = cupo.Cupo.PublicId, limit = cupo.Limite, available = cupo.Disponible }));

        var medida = await concentracion.MedirAsync(persona.Id, valor, false, fecha, null, ct);
        if (medida.IsFailure)
            v.Add(new VerificacionDeVenta(Concentracion, false, medida.Error.Code, medida.Error.Message));
        else if (medida.Value.Error is { } excedida)
            v.Add(new VerificacionDeVenta(Concentracion, false, excedida.Code, excedida.Message,
                new { limit = medida.Value.Excedida!.Limite, current = medida.Value.Excedida.Actual, requested = valor, excess = medida.Value.Excedida.Exceso }));
        else
            v.Add(new VerificacionDeVenta(Concentracion, true, Evidence: new { limit = medida.Value.PorDeudor.Limite, current = medida.Value.PorDeudor.Actual }));

        v.Add(usura is null || usura.Pasa
            ? new VerificacionDeVenta(Usura, true, Evidence: usura is null ? null : new { effective = usura.EfectivaConCargos, usury = usura.Usura, usuryValue = usura.UsuraPublicId })
            : new VerificacionDeVenta(Usura, false, ErroresDeOriginacion.CodigoUsuraExcedida,
                $"La tasa efectiva de la venta supera la usura vigente a su fecha.",
                new { effective = usura.EfectivaConCargos, usury = usura.Usura, usuryValue = usura.UsuraPublicId }));

        v.Add(fueraDeRango
            ? new VerificacionDeVenta(LineaComercial, false, ErroresDeVentasACredito.CodigoFueraDeLaLinea,
                $"La venta está fuera de los rangos de la línea {venta.Linea.Code} (monto, plazo o cuotas).", new { line = venta.Linea.Code })
            : new VerificacionDeVenta(LineaComercial, true, Evidence: new { line = venta.Linea.Code, version = venta.Version.Version.PublicId }));

        var consulta = await listas.ConsultarAsync(new PedidoDeConsultaDeListas(persona.Id, ScreeningContext.Sale), ct);
        v.Add(consulta.Bloquea
            ? new VerificacionDeVenta(Listas, false, ErroresDeOriginacion.CodigoCoincidenciaEnListas,
                "La consulta de listas restrictivas coincidió: el oficial de cumplimiento debe revisarla.", new { screening = consulta.Resultado.PublicId })
            : new VerificacionDeVenta(Listas, true, Evidence: new { screening = consulta.Resultado.PublicId, outcome = consulta.Resultado.Outcome.ToString() }));

        var autorizacion = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.CreditProcessing, null, ct);
        v.Add(autorizacion is { Autoriza: true }
            ? new VerificacionDeVenta(Autorizacion, true, Evidence: new { consent = autorizacion.ConsentPublicId })
            : new VerificacionDeVenta(Autorizacion, false, ErroresDeOriginacion.CodigoSinAutorizacion,
                "El comprador no tiene autorización vigente de tratamiento de datos para crédito."));

        return new EvaluacionDeVenta(v);
    }

    /// <summary>¿La falla de una evaluación es de las que ninguna atribución exceptúa (FR-038)?</summary>
    public static MotivoNoExceptuable? NoExceptuable(string? codigo) =>
        codigo is not null && NoExceptuablesPorCodigo.TryGetValue(codigo, out var m) ? m : null;
}
