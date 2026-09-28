namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La marca de «Cartera no habilitada» (feature 012, I3, T651; contracts/api.md §23.3; T32, §2.16): lo que responde
/// <see cref="IConsultasDeCartera.EstadoCrediticioAsync"/> mientras no exista el destino <c>Lending</c> ni fecha en
/// <c>Cartera.IntegracionHabilitadaDesde</c>. Quien la recibe aplica el crédito provisional: persona identificada y activa,
/// aprobación por monto y pago «pendiente de validar».
/// </summary>
public sealed record CarteraNoHabilitada() : EstadoCrediticioDto("NotEnabled", null, [], null)
{
    /// <summary>La única instancia (no lleva datos).</summary>
    public static CarteraNoHabilitada Instancia { get; } = new();

    public override bool LendingEnabled => false;

    /// <summary>El texto que ve quien consulta una venta a crédito mientras IC está pendiente (§23.2).</summary>
    public const string TextoPendiente = "Pendiente: el destino aún no está disponible (IC)";
}
