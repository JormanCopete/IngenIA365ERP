using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de las líneas de crédito (feature 013, US1, T0359; contracts/api.md §3.1–§3.3): la lista y el detalle con su historial,
/// el alta con su primera versión, las versiones nuevas, cerrar, reabrir y borrar con motivo, la consulta de usura, el puntaje, las
/// modalidades del catálogo nacional, las equivalencias de Comercio y la plantilla <c>lending.credit-lines</c>. Sin <c>Authorization</c> a
/// mano (la pone el handler de la sesión); toda escritura lleva su <see cref="ClaveDeOperacion"/> —revisar y aplicar la plantilla son dos
/// operaciones, cada una con la suya—; las consultas no, y <c>usury-check</c> tampoco aunque vaya por POST: no escribe (api.md §1.5). (nuevo)
/// </summary>
public sealed class LineasDeCreditoClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeLineas = GobiernoDeCarteraClient.Base + "/credit-lines";
    public const string RutaDeModalidades = GobiernoDeCarteraClient.Base + "/modalities";
    public const string RutaDeEquivalencias = GobiernoDeCarteraClient.Base + "/commercial-term-equivalences";

    /// <summary>La clave del dueño de los soportes del concepto tributario (<c>AdjuntosDeModulo.SoporteTributarioDeLinea</c>).</summary>
    public const string DuenoDelSoporteTributario = "CreditLineTaxSupport";

    private readonly InventarioClient _plantillas = new(http, auth);

    // ---------------------------------------------------------------------------------------------- líneas --

    /// <summary>Las líneas del alcance de quien pregunta, por código.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<LineaDeCreditoResumenDto>>> LineasAsync(FiltrosDeLineas? filtros = null, CancellationToken ct = default)
    {
        var f = filtros ?? new FiltrosDeLineas();
        return Enviar<IReadOnlyList<LineaDeCreditoResumenDto>>(HttpMethod.Get, ConQuery(RutaDeLineas, Query(
            ("status", Texto(f.Status)), ("modality", f.Modality), ("commercialOrigin", Texto(f.CommercialOrigin)),
            ("includeClosed", Texto(f.IncludeClosed)))), null, null, ct);
    }

    /// <summary>La línea con la versión vigente a <paramref name="fecha"/> (hoy si no viene) y su historial. Fuera de alcance, el 404 genérico.</summary>
    public Task<ResultadoDeInventario<LineaDeCreditoDto>> LineaAsync(Guid linea, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<LineaDeCreditoDto>(HttpMethod.Get, ConQuery($"{RutaDeLineas}/{linea}", Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>Una versión completa del historial.</summary>
    public Task<ResultadoDeInventario<VersionDeLineaDto>> VersionAsync(Guid linea, Guid version, CancellationToken ct = default) =>
        Enviar<VersionDeLineaDto>(HttpMethod.Get, $"{RutaDeLineas}/{linea}/versions/{version}", null, null, ct);

    /// <summary>
    /// Crea la línea con su primera versión. Si una tasa supera la usura responde 422 <c>Lending.Usury.Exceeded</c> con la usura, la
    /// modalidad, la fecha y el campo (<c>data.errors[].field</c>), y no guarda nada.
    /// </summary>
    public Task<ResultadoDeInventario<LineaDeCreditoDto>> CrearAsync(CrearLineaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LineaDeCreditoDto>(HttpMethod.Post, RutaDeLineas, request, clave, ct);

    /// <summary>Una versión nueva desde una fecha (nunca antes de hoy): la anterior rige hasta la víspera y los créditos vivos no cambian.</summary>
    public Task<ResultadoDeInventario<VersionDeLineaDto>> NuevaVersionAsync(Guid linea, VersionDeLineaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VersionDeLineaDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/versions", request, clave, ct);

    /// <summary>Cierra la línea a créditos nuevos, con motivo; los créditos vivos no cambian.</summary>
    public Task<ResultadoDeInventario<LineaDeCreditoDto>> CerrarAsync(Guid linea, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LineaDeCreditoDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/close", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>Reabre una línea cerrada: revalida la usura del día.</summary>
    public Task<ResultadoDeInventario<LineaDeCreditoDto>> ReabrirAsync(Guid linea, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LineaDeCreditoDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/reopen", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>
    /// Borrado lógico, sólo sin solicitudes, créditos, cupos ni mensajes; si no, 422 <c>Lending.CreditLine.HasLoans</c> con
    /// <c>data.alternatives = ["Close"]</c>. El motivo va en la query (<c>DELETE</c> sin cuerpo).
    /// </summary>
    public Task<ResultadoDeInventario<EmptyResponse>> BorrarAsync(Guid linea, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Delete, ConQuery($"{RutaDeLineas}/{linea}", Query(("reason", motivo))), null, clave, ct);

    /// <summary>La usura de la versión vigente: efectiva con y sin cargos, usura, holgura y la explicación. No escribe: va sin clave.</summary>
    public Task<ResultadoDeInventario<ConsultaDeUsuraDto>> ConsultarUsuraAsync(Guid linea, ConsultaDeUsuraRequest? request = null, CancellationToken ct = default) =>
        Enviar<ConsultaDeUsuraDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/usury-check", request ?? new ConsultaDeUsuraRequest(), null, ct);

    // --------------------------------------------------------------------------------------------- puntaje --

    public Task<ResultadoDeInventario<PuntajeDeLineaDto>> PuntajeAsync(Guid linea, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<PuntajeDeLineaDto>(HttpMethod.Get, ConQuery($"{RutaDeLineas}/{linea}/scoring", Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>Una versión que copia la vigente y cambia sólo el puntaje (<c>Lending.Scoring.Manage</c>).</summary>
    public Task<ResultadoDeInventario<VersionDeLineaDto>> FijarPuntajeAsync(Guid linea, PuntajeRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VersionDeLineaDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/scoring", request, clave, ct);

    // ------------------------------------------------------------------------------ modalidades y equivalencias --

    /// <summary>Las modalidades del documento nacional vigente a la fecha, con sus sub-segmentos y la clave de usura de cada combinación.</summary>
    public Task<ResultadoDeInventario<CatalogoDeModalidadesDto>> ModalidadesAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<CatalogoDeModalidadesDto>(HttpMethod.Get, ConQuery(RutaDeModalidades, Query(("date", Texto(fecha)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<EquivalenciaDeComercioDto>>> EquivalenciasAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<EquivalenciaDeComercioDto>>(HttpMethod.Get, ConQuery(RutaDeEquivalencias, Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>Registra una versión de cada equivalencia desde una fecha, con motivo.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<EquivalenciaDeComercioDto>>> FijarEquivalenciasAsync(EquivalenciasDeComercioRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<EquivalenciaDeComercioDto>>(HttpMethod.Put, RutaDeEquivalencias, request, clave, ct);

    // ------------------------------------------------------------------------------------------- plantilla --

    /// <summary>La plantilla <c>lending.credit-lines</c> vacía (seis hojas, encabezados en la fila 1).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarPlantillaAsync(CancellationToken ct = default) =>
        _plantillas.DescargarPlantillaAsync(RutaDeLineas, conDatos: false, ct);

    /// <summary>Revisa sin guardar: las mismas reglas que el alta y la versión, con hoja, fila y columna de cada error.</summary>
    public Task<ResultadoDeInventario<ResultadoDeImportacionDto>> RevisarPlantillaAsync(string archivo, byte[] contenido, string? motivo,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        _plantillas.RevisarPlantillaAsync(RutaDeLineas, archivo, contenido, motivo, clave, ct);

    /// <summary>Todo o nada: con un error responde 422 <c>Import.Invalid</c> con la revisión en <c>data</c>.</summary>
    public Task<ResultadoDeInventario<ResultadoDeImportacionDto>> AplicarPlantillaAsync(string archivo, byte[] contenido, string? motivo,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        _plantillas.AplicarPlantillaAsync(RutaDeLineas, archivo, contenido, motivo, clave, ct);

    // ----------------------------------------------------------------------------------------- de apoyo --

    /// <summary>
    /// Lo que el 422 de usura dice en español: «la efectiva con cargos 27,1 % EA supera la usura de 25,6 % de CONSUMO_Y_ORDINARIO el
    /// 2026-11-01 por 1,5 puntos». Nulo si el error no es de usura.
    /// </summary>
    public static string? ExplicarUsuraExcedida<T>(ResultadoDeInventario<T> r)
    {
        if (r.ErrorCode != "Lending.Usury.Exceeded") return null;
        var efectiva = r.Dato<decimal?>("effectiveRate");
        var usura = r.Dato<decimal?>("usuryRate");
        var clave = r.Dato<string>("modalityKey");
        var fecha = r.Dato<string>("date");
        if (efectiva is null || usura is null) return r.ErrorMessage;
        return $"La tasa efectiva con cargos ({Porcentaje(efectiva.Value)} EA) supera la usura de {clave ?? "la modalidad"} "
               + $"({Porcentaje(usura.Value)} EA){(fecha is null ? "" : $" del {fecha}")} por {Puntos(efectiva.Value - usura.Value)}.";
    }

    /// <summary>
    /// Lo que un rechazo de la API dice, listo para mostrar en el editor: la usura excedida en español, el mensaje y, si vienen, los campos
    /// señalados (<c>data.errors[].field</c>) y los motivos de la importación.
    /// </summary>
    public static IReadOnlyList<string> Explicar<T>(ResultadoDeInventario<T> r)
    {
        var lineas = new List<string> { ExplicarUsuraExcedida(r) ?? r.ErrorMessage ?? "No se pudo guardar." };
        var errores = r.Dato<List<System.Text.Json.JsonElement>>("errors") ?? [];
        foreach (var e in errores)
        {
            var campo = e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty("field", out var f) ? f.GetString() : null;
            var mensaje = e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty("message", out var m) ? m.GetString() : null;
            if (campo is null && mensaje is null) continue;
            var texto = $"{(campo is null ? "" : $"Campo {campo}")}{(campo is not null && mensaje is not null ? ": " : "")}{mensaje}";
            if (!lineas.Contains(texto)) lineas.Add(texto);
        }
        return lineas;
    }

    /// <summary>Una fracción como porcentaje en español con hasta cuatro decimales (0,2562 → «25,62 %»).</summary>
    public static string Porcentaje(decimal fraccion) =>
        (fraccion * 100m).ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("es-CO")) + " %";

    /// <summary>Una diferencia de fracciones en puntos porcentuales.</summary>
    public static string Puntos(decimal fraccion) =>
        (fraccion * 100m).ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("es-CO")) + " puntos";

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
