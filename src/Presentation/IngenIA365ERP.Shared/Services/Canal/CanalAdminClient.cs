using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Canal;

/// <summary>
/// Cliente tipado de la administración del canal digital (feature 021, US1, T050–T053; contracts/erp-administracion.md): la
/// configuración y su encendido, los textos legales, los destinos de pago con sus credenciales, los conceptos publicados y las
/// habilitaciones. Sin <c>Authorization</c> a mano (la pone el handler de la sesión, <c>ElTokenDeSesionLoPoneElHandler</c>); toda
/// escritura lleva su <see cref="ClaveDeOperacion"/> (las rutas exigen <c>Idempotency-Key</c>), las consultas no. La carga masiva de
/// habilitaciones la hace el componente <c>ImportarPlantilla</c> sobre <see cref="RutaDeHabilitaciones"/>. (nuevo)
/// </summary>
public sealed class CanalAdminClient(HttpClient http, CentralAuthClient auth)
{
    public const string Base = "/api/channel";
    public const string RutaDeHabilitaciones = Base + "/enablements";

    // ---------------------------------------------------------------------------------------------- configuración --

    public Task<ResultadoDeInventario<ConfiguracionDelCanalDto>> ConfiguracionAsync(CancellationToken ct = default) =>
        Enviar<ConfiguracionDelCanalDto>(HttpMethod.Get, $"{Base}/settings", null, null, ct);

    /// <summary>Guarda la configuración como una versión nueva; no enciende ni apaga.</summary>
    public Task<ResultadoDeInventario<ConfiguracionDelCanalDto>> GuardarConfiguracionAsync(ConfiguracionDelCanalRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ConfiguracionDelCanalDto>(HttpMethod.Put, $"{Base}/settings", r, clave, ct);

    /// <summary>
    /// Enciende el canal. Responde 422 <c>Channel.Settings.DisplayNameMissing</c> sin el nombre guardado y
    /// <c>Channel.Settings.LegalTextsMissing</c> sin los términos y la política de datos publicados.
    /// </summary>
    public Task<ResultadoDeInventario<ConfiguracionDelCanalDto>> EncenderAsync(ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConfiguracionDelCanalDto>(HttpMethod.Post, $"{Base}/settings/enable", null, clave, ct);

    public Task<ResultadoDeInventario<ConfiguracionDelCanalDto>> ApagarAsync(ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConfiguracionDelCanalDto>(HttpMethod.Post, $"{Base}/settings/disable", null, clave, ct);

    // -------------------------------------------------------------------------------------------- textos legales --

    /// <summary>Sin tipo, la versión vigente de cada tipo; con tipo (por nombre: <c>Terms</c>…), toda su historia, la más nueva primero.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<TextoLegalDto>>> TextosLegalesAsync(string? tipo = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<TextoLegalDto>>(HttpMethod.Get, ConQuery($"{Base}/legal-texts", Query(("kind", tipo))), null, null, ct);

    /// <summary>Publica una versión nueva; las publicadas no se editan.</summary>
    public Task<ResultadoDeInventario<TextoLegalDto>> PublicarTextoLegalAsync(TextoLegalRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TextoLegalDto>(HttpMethod.Post, $"{Base}/legal-texts", r, clave, ct);

    // ---------------------------------------------------------------------------------------- destinos de pago --

    public Task<ResultadoDeInventario<IReadOnlyList<DestinoDePagoDto>>> DestinosAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<DestinoDePagoDto>>(HttpMethod.Get, $"{Base}/destinations", null, null, ct);

    /// <summary>Crea (sin <paramref name="destino"/>) o cambia un destino. Exige un convenio de recaudo activo de clase pasarela.</summary>
    public Task<ResultadoDeInventario<DestinoDePagoDto>> GuardarDestinoAsync(Guid? destino, DestinoDePagoRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DestinoDePagoDto>(destino is null ? HttpMethod.Post : HttpMethod.Put,
            destino is null ? $"{Base}/destinations" : $"{Base}/destinations/{destino}", r, clave, ct);

    /// <summary>Envía las credenciales del sitio al canal, que las cifra y las verifica; la respuesta trae el estado de la verificación.</summary>
    public Task<ResultadoDeInventario<DestinoDePagoDto>> GuardarCredencialesAsync(Guid destino, CredencialesDePasarelaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DestinoDePagoDto>(HttpMethod.Put, $"{Base}/destinations/{destino}/credentials", r, clave, ct);

    /// <summary>Los convenios de recaudo activos de clase pasarela (de Recaudo), los únicos que admite un destino.</summary>
    public async Task<ResultadoDeInventario<IReadOnlyList<ConvenioDto>>> ConveniosDePasarelaAsync(CancellationToken ct = default)
    {
        var r = await Enviar<IReadOnlyList<ConvenioDto>>(HttpMethod.Get,
            ConQuery("/api/receipts/collection-agreements", Query(("onlyActive", "true"))), null, null, ct);
        return r.IsSuccess
            ? r with { Value = (r.Value ?? []).Where(c => c.Kind == EnumsDelCanal.ConvenioDePasarela && c.IsActive).ToList() }
            : r;
    }

    // ----------------------------------------------------------------------------------------------- conceptos --

    public Task<ResultadoDeInventario<IReadOnlyList<ConceptoDelCanalDto>>> ConceptosAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ConceptoDelCanalDto>>(HttpMethod.Get, $"{Base}/concepts", null, null, ct);

    public Task<ResultadoDeInventario<ConceptoDelCanalDto>> GuardarConceptoAsync(Guid? concepto, ConceptoDelCanalRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ConceptoDelCanalDto>(concepto is null ? HttpMethod.Post : HttpMethod.Put,
            concepto is null ? $"{Base}/concepts" : $"{Base}/concepts/{concepto}", r, clave, ct);

    /// <summary>Retira el concepto del canal (baja lógica): deja de verse y de pagarse desde la siguiente consulta.</summary>
    public Task<ResultadoDeInventario<object>> RetirarConceptoAsync(Guid concepto, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<object>(HttpMethod.Delete, $"{Base}/concepts/{concepto}", null, clave, ct);

    /// <summary>
    /// Las líneas del catálogo común de Cartera de una naturaleza (<c>Placement</c>, <c>Contribution</c>, <c>Deposit</c>) y, en captación,
    /// de una clase (<c>Contractual</c>, <c>Permanent</c>). Exige poder ver las líneas de Cartera.
    /// </summary>
    public Task<ResultadoDeInventario<IReadOnlyList<LineaParaElCanalDto>>> LineasAsync(string naturaleza, string? claseDeCaptacion,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<LineaParaElCanalDto>>(HttpMethod.Get, ConQuery("/api/lending/lines",
            Query(("nature", naturaleza), ("depositClass", claseDeCaptacion))), null, null, ct);

    // ------------------------------------------------------------------------------------------- habilitaciones --

    /// <summary>Las habilitaciones por página; <paramref name="estado"/> por nombre (<c>Enabled</c>, <c>Disabled</c>), <paramref name="texto"/> busca en documento y nombres.</summary>
    public Task<ResultadoDeInventario<PaginaDeHabilitacionesDto>> HabilitacionesAsync(string? estado, string? texto, int pagina, int tamano,
        CancellationToken ct = default) =>
        Enviar<PaginaDeHabilitacionesDto>(HttpMethod.Get, ConQuery(RutaDeHabilitaciones, Query(("status", estado),
            ("q", string.IsNullOrWhiteSpace(texto) ? null : texto.Trim()), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>
    /// Habilita a una persona. Sin «permitir no asociados» en la configuración, responde 422 <c>Channel.Enablement.NotAssociate</c> si no es
    /// asociada activa.
    /// </summary>
    public Task<ResultadoDeInventario<HabilitacionDelCanalDto>> HabilitarAsync(Guid persona, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<HabilitacionDelCanalDto>(HttpMethod.Post, RutaDeHabilitaciones, new { personPublicId = persona }, clave, ct);

    /// <summary>Inhabilita con motivo (obligatorio, hasta 300 caracteres).</summary>
    public Task<ResultadoDeInventario<HabilitacionDelCanalDto>> InhabilitarAsync(Guid persona, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<HabilitacionDelCanalDto>(HttpMethod.Post, $"{RutaDeHabilitaciones}/{persona}/disable", new { reason = motivo }, clave, ct);

    /// <summary>Vuelve a habilitar a una persona inhabilitada (con la fecha de hoy).</summary>
    public Task<ResultadoDeInventario<HabilitacionDelCanalDto>> RehabilitarAsync(Guid persona, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<HabilitacionDelCanalDto>(HttpMethod.Post, $"{RutaDeHabilitaciones}/{persona}/enable", null, clave, ct);

    // ---------------------------------------------------------------------------------------- novedades de contacto --

    public const string RutaDeNovedades = Base + "/contact-updates";

    /// <summary>Las novedades de contacto que reportó el canal (FR-024): <paramref name="pendientes"/> nulo = todas. El valor nuevo llega enmascarado.</summary>
    public Task<ResultadoDeInventario<PaginaDeNovedadesDto>> NovedadesDeContactoAsync(bool? pendientes, int pagina, int tamano, CancellationToken ct = default) =>
        Enviar<PaginaDeNovedadesDto>(HttpMethod.Get, ConQuery(RutaDeNovedades, Query(("pending", pendientes is null ? null : pendientes.Value ? "true" : "false"),
            ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>El valor completo de la novedad y el que la persona tiene hoy. Queda en la auditoría como lectura sensible.</summary>
    public Task<ResultadoDeInventario<NovedadDeContactoDetalleDto>> NovedadDeContactoAsync(Guid novedad, CancellationToken ct = default) =>
        Enviar<NovedadDeContactoDetalleDto>(HttpMethod.Get, $"{RutaDeNovedades}/{novedad}", null, null, ct);

    /// <summary>Marca la novedad como atendida. No cambia la persona: eso se hace en Personas.</summary>
    public Task<ResultadoDeInventario<NovedadDeContactoDto>> AtenderNovedadAsync(Guid novedad, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<NovedadDeContactoDto>(HttpMethod.Post, $"{RutaDeNovedades}/{novedad}/acknowledge", null, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
