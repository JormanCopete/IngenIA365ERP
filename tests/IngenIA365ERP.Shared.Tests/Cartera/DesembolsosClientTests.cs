using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0458 (feature 013, US3; contracts/api.md §1.5, §6, §8.4, §9, §20): <see cref="DesembolsosClient"/>, <see cref="GarantiasClient"/>,
/// <see cref="ArchivosDeDesembolsoClient"/> y <see cref="DoblesDePruebaClient"/>. Desembolsar, anular la orden, el giro nuevo, anular el crédito,
/// las garantías, el pagaré (salvo actualizar su estado), generar y anular el archivo y el resultado de la tesorería de prueba llevan su
/// <c>Idempotency-Key</c>; las vistas previas, actualizar el pagaré, el enlace firmado y las listas no; ninguna pone <c>Authorization</c>. Sin la
/// ruta de los dobles (producción), <see cref="DoblesDePruebaClient.DisponiblesAsync"/> responde nulo. (nuevo)
/// </summary>
public class DesembolsosClientTests
{
    private static readonly Guid Solicitud = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Orden = Guid.Parse("22222222-3333-4444-5555-666666666666");
    private static readonly Guid Credito = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private static readonly Guid Otro = Guid.Parse("44444444-5555-6666-7777-888888888888");

    private readonly ServidorDeCartera _servidor = new();
    private readonly DesembolsosClient _desembolsos;
    private readonly GarantiasClient _garantias;
    private readonly ArchivosDeDesembolsoClient _archivos;

    public DesembolsosClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _desembolsos = new DesembolsosClient(http, auth);
        _garantias = new GarantiasClient(http, auth);
        _archivos = new ArchivosDeDesembolsoClient(http, auth);
    }

    public sealed record Clientes(DesembolsosClient D, GarantiasClient G, ArchivosDeDesembolsoClient A);

    private Clientes Todos => new(_desembolsos, _garantias, _archivos);

    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("desembolsar", "POST", "/api/lending/disbursements", (c, k) =>
            c.D.DesembolsarAsync(new DesembolsoRequest { ApplicationPublicId = Solicitud, Form = "Check" }, k));
        yield return E("anular orden", "POST", $"/api/lending/disbursement-orders/{Orden}/void", (c, k) => c.D.AnularOrdenAsync(Orden, "Cuenta errada", k));
        yield return E("giro nuevo", "POST", $"/api/lending/disbursement-orders/{Orden}/reissue", (c, k) =>
            c.D.GiroNuevoAsync(Orden, new GiroNuevoRequest("Check", null, "Rechazada por el banco"), k));
        yield return E("anular crédito", "POST", $"/api/lending/loans/{Credito}/void", (c, k) => c.D.AnularCreditoAsync(Credito, "Desistió", k));
        yield return E("garantía", "POST", "/api/lending/collaterals", (c, k) =>
            c.G.RegistrarGarantiaAsync(new GarantiaRequest { OwnerPublicId = Solicitud, Description = "Vehículo", Value = 30_000_000m }, k));
        yield return E("editar garantía", "PUT", $"/api/lending/collaterals/{Otro}", (c, k) =>
            c.G.EditarGarantiaAsync(Otro, new GarantiaRequest { OwnerPublicId = Solicitud, Description = "Vehículo", Value = 31_000_000m }, k));
        yield return E("estado", "POST", $"/api/lending/collaterals/{Otro}/status", (c, k) =>
            c.G.CambiarEstadoAsync(Otro, new EstadoDeGarantiaRequest("Valid", "Registrada en tránsito"), k));
        yield return E("liberar", "POST", $"/api/lending/collaterals/{Otro}/release", (c, k) => c.G.LiberarAsync(Otro, "Crédito cancelado", k));
        yield return E("pagaré", "POST", "/api/lending/promissory-notes", (c, k) =>
            c.G.CrearPagareAsync(new PagareRequest("Application", Solicitud, "Dematerialized"), k));
        yield return E("custodia", "POST", $"/api/lending/promissory-notes/{Otro}/physical-custody", (c, k) =>
            c.G.CustodiaAsync(Otro, new CustodiaDePagareRequest(new DateTime(2026, 10, 11, 9, 0, 0, DateTimeKind.Utc), "Caja fuerte", Orden), k));
        yield return E("anular pagaré", "POST", $"/api/lending/promissory-notes/{Otro}/void", (c, k) => c.G.AnularPagareAsync(Otro, "Crédito cancelado", k));
        yield return E("generar", "POST", "/api/lending/disbursement-files", (c, k) => c.A.GenerarAsync(new ArchivoDeDesembolsoRequest(Otro, Credito, [Orden]), k));
        yield return E("anular archivo", "POST", $"/api/lending/disbursement-files/{Otro}/void", (c, k) => c.A.AnularAsync(Otro, "Formato errado", k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<Clientes, ClaveDeOperacion, Task> accion) => [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta, Func<Clientes, ClaveDeOperacion, Task> accion)
    {
        await accion(Todos, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo);
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task Las_vistas_previas_el_pagare_el_enlace_y_las_listas_van_sin_clave()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0 });

        await _desembolsos.VistaPreviaAsync(new DesembolsoRequest { ApplicationPublicId = Solicitud });
        await _garantias.ActualizarPagareAsync(Otro);
        await _archivos.VistaPreviaAsync(new ArchivoDeDesembolsoRequest(Otro, Credito));
        await _archivos.EnlaceAsync(Otro);
        await _desembolsos.OrdenesAsync("Issued", Credito);
        await _archivos.CuentasDeOrigenAsync(Otro);

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("POST", $"/api/lending/applications/{Solicitud}/disbursement-preview"),
            ("POST", $"/api/lending/promissory-notes/{Otro}/refresh"),
            ("POST", "/api/lending/disbursement-files/preview"),
            ("POST", $"/api/lending/disbursement-files/{Otro}/download-link"),
            ("GET", $"/api/lending/disbursement-orders?status=Issued&loanPublicId={Credito}&page=1"),
            ("GET", $"/api/lending/disbursement-files/source-accounts?formatPublicId={Otro}"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }
}
