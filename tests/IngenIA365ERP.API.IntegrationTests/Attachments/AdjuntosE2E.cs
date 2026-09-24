using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Feature 011: lo que comparten las e2e de subida y descarga directas. Una cooperativa aislada con la
/// contabilidad iniciada y un borrador de comprobante al que se le suben soportes; y el recorrido
/// completo de una subida tal como lo hace el navegador (adjuntos.js): pedir, enviar al almacén, confirmar.
/// </summary>
public static class AdjuntosE2E
{
    public sealed record Cooperativa(string Token, Guid TenantPublicId, Guid Borrador);

    public sealed record Subida(Guid Adjunto, HttpStatusCode EstadoDelAlmacen, JsonElement? Confirmacion);

    /// <summary>El «navegador»: habla con el almacén, no con la API.</summary>
    public static readonly HttpClient Navegador = new();

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<Cooperativa>> Preparadas = [];
    private static readonly object Cerrojo = new();

    public static Task<Cooperativa> CooperativaAsync(CentralIdentityApiFixture fx, string sufijo)
    {
        lock (Cerrojo)
        {
            if (!Preparadas.TryGetValue((fx, sufijo), out var tarea))
            {
                tarea = PrepararAsync(fx, sufijo);
                Preparadas[(fx, sufijo)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Cooperativa> PrepararAsync(CentralIdentityApiFixture fx, string sufijo)
    {
        var coop = await CooperativaAisladaAsync(fx, sufijo, 2026);
        using var http = fx.CreateClient();
        await CrearAuxiliarAsync(http, coop.TokenAdmin, GastoAseo, "Aseo y elementos");
        await CrearAuxiliarAsync(http, coop.TokenAdmin, CajaPrincipal, "Caja menor");
        var borrador = await BorradorAsync(http, coop.TokenAdmin, new DateOnly(2026, 9, 5), "Compra de papelería", new object[]
        {
            Linea(GastoAseo, coop.SucursalPrincipal, 120_000m, 0m),
            Linea(CajaPrincipal, coop.SucursalPrincipal, 0m, 120_000m),
        }, admiteErrores: true);
        return new Cooperativa(coop.TokenAdmin, coop.TenantPublicId, borrador);
    }

    public static byte[] Pdf(int tamano)
    {
        var contenido = RandomNumberGenerator.GetBytes(tamano);
        "%PDF-1.7\n"u8.CopyTo(contenido);
        return contenido;
    }

    public static Task<HttpResponseMessage> PedirSubidaAsync(HttpClient api, string token, Guid dueno, byte[] contenido,
        string nombre = "Factura 1234 – Núñez.pdf", string tipo = "application/pdf", string ownerEntityType = "AccountingDocument") =>
        EnviarAsync(api, token, HttpMethod.Post, "/api/attachments/uploads", new
        {
            ownerEntityType, ownerEntityPublicId = dueno, fileName = nombre, contentType = tipo,
            sizeBytes = contenido.LongLength, sha256Base64 = Convert.ToBase64String(SHA256.HashData(contenido)),
        });

    /// <summary>Pedir → enviar al almacén (todos los campos, el archivo al final) → confirmar.</summary>
    public static async Task<Subida> SubirAsync(HttpClient api, string token, Guid dueno, byte[] contenido,
        string nombre = "Factura 1234 – Núñez.pdf", string tipo = "application/pdf")
    {
        using var pedido = await PedirSubidaAsync(api, token, dueno, contenido, nombre, tipo);
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");

        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, nombre);
        using var almacen = await Navegador.PostAsync(subida.GetProperty("url").GetString(), formulario);
        if (!almacen.IsSuccessStatusCode) return new Subida(adjunto, almacen.StatusCode, null);

        using var confirmacion = await EnviarAsync(api, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return new Subida(adjunto, almacen.StatusCode, await LeerAsync(confirmacion));
    }
}
