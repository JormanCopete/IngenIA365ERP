using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Tesoreria;
using IngenIA365ERP.Shared.Tests.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Tesoreria;

/// <summary>
/// Feature 015, fase 47 (T0482): el ciclo del lote por <see cref="LotesClient"/> —crear, cambiar órdenes, enviar a aprobación, revisar una orden, aprobar, rechazar,
/// generar el archivo, enviado por portal, marcar enviado, anular el archivo— va a sus rutas con su clave; ver, la vista previa, listar y descargar no la llevan. Ninguna
/// pone <c>Authorization</c>. <b>Desviación</b>: como en la caja, el estado del lote vive en la pantalla (<c>Pages/Tesoreria/Lotes.razor</c>); se prueba el cliente.
/// Prueba escrita, sin correr.
/// </summary>
public class LotesEstadoTests
{
    private static readonly Guid Lote = Guid.Parse("15151515-1111-0000-0000-000000000001");
    private static readonly Guid Cuenta = Guid.Parse("15151515-1111-0000-0000-000000000002");
    private static readonly Guid Orden = Guid.Parse("15151515-1111-0000-0000-000000000003");
    private static readonly Guid Archivo = Guid.Parse("15151515-1111-0000-0000-000000000004");

    private readonly ServidorDeCartera _servidor = new();
    private readonly LotesClient _lotes;

    public LotesEstadoTests()
    {
        var (http, auth) = _servidor.Sesion();
        _lotes = new LotesClient(http, auth);
    }

    public static IEnumerable<object[]> Escrituras()
    {
        const string L = "/api/treasury/payment-batches";
        const string A = "/api/treasury/payment-files";
        yield return E("crear", "POST", L, (c, k) => c.CrearAsync(new CrearLoteRequest(Cuenta, 1, new DateOnly(2026, 12, 1), [Orden], null, null, null), k));
        yield return E("cambiar órdenes", "PUT", $"{L}/{Lote}/items", (c, k) => c.CambiarOrdenesAsync(Lote, new ItemsDeLoteRequest([Orden], []), k));
        yield return E("enviar a aprobación", "POST", $"{L}/{Lote}/submit", (c, k) => c.EnviarAAprobacionAsync(Lote, k));
        yield return E("revisar", "POST", $"{L}/{Lote}/items/{Orden}/review", (c, k) => c.RevisarAsync(Lote, Orden, "visto", k));
        yield return E("aprobar", "POST", $"{L}/{Lote}/approve", (c, k) => c.AprobarAsync(Lote, null, k));
        yield return E("rechazar", "POST", $"{L}/{Lote}/reject", (c, k) => c.RechazarAsync(Lote, "No corresponde", k));
        yield return E("generar archivo", "POST", $"{L}/{Lote}/file", (c, k) => c.GenerarArchivoAsync(Lote, k));
        yield return E("enviado por portal", "POST", $"{L}/{Lote}/sent", (c, k) => c.EnviadoPorPortalAsync(Lote, new EnvioAlBancoRequest("Tesorera", "Gerente"), k));
        yield return E("marcar enviado", "POST", $"{A}/{Archivo}/sent", (c, k) => c.MarcarEnviadoAsync(Archivo, new EnvioAlBancoRequest("Tesorera", "Gerente"), k));
        yield return E("anular archivo", "POST", $"{A}/{Archivo}/void", (c, k) => c.AnularArchivoAsync(Archivo, "Archivo con error", k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<LotesClient, ClaveDeOperacion, Task> accion) => [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta, Func<LotesClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_lotes, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo, nombre);
        vista.Ruta.Should().Be(ruta, nombre);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Ver_listar_la_vista_previa_y_descargar_van_sin_clave()
    {
        await _lotes.LotesAsync();
        await _lotes.LoteAsync(Lote);
        await _lotes.VistaPreviaAsync(Lote);
        await _lotes.ArchivosAsync(Lote);
        await _lotes.DescargarAsync(Archivo);

        _servidor.Vistas.Should().HaveCount(5).And.OnlyContain(v => v.Clave == null && v.Metodo == "GET");
    }
}
