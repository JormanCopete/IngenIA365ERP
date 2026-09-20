using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.Endpoints.Reports;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.API.IntegrationTests.Reports;

/// <summary>
/// La entrega de informes y el binding de los filtros contables (feature 009 E2), sin contenedores.
///
/// <para>
/// Hasta el 2026-09-20 <see cref="EntregaDeInformes"/> respondía 400 a cualquier fallo del handler:
/// un tercero inexistente y un formato mal escrito eran la misma respuesta. Ahora sigue la regla
/// del sobre (<c>*.NotFound</c> 404, <c>Validation.*</c> 400, resto 422) y el formato inválido
/// sigue siendo 400 porque la prueba e2e de nómina lo afirma.
/// </para>
/// </summary>
public class EntregaDeInformesTests
{
    private static readonly TablaExportable Tabla = new("Prueba", "", [new ColumnaExportable("A")], [new FilaExportable(["1"])], null, []);

    private static async Task<(int Status, string? Code)> FalloAsync(string codigo)
    {
        var r = await EntregaDeInformes.EntregarAsync(Result.Failure<TablaExportable>(new Error(codigo, "mensaje")), "json", "x");
        var status = r.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode;
        var cuerpo = r.Should().BeAssignableTo<IValueHttpResult>().Which.Value;
        var code = cuerpo?.GetType().GetProperty("code")?.GetValue(cuerpo) as string;
        return (status ?? 0, code);
    }

    [Fact]
    public async Task UnNotFoundDelHandlerEs404()
    {
        var (status, code) = await FalloAsync("Accounting.Person.NotFound");
        status.Should().Be(StatusCodes.Status404NotFound);
        code.Should().Be("Accounting.Person.NotFound");
    }

    [Fact]
    public async Task UnaReglaDeNegocioEs422()
    {
        var (status, _) = await FalloAsync("Accounting.Report.PersonRequired");
        status.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task UnaValidacionEs400()
    {
        var (status, _) = await FalloAsync("Validation.Level");
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ElErrorConDatosLlevaData()
    {
        var error = new ErrorConDatos("Accounting.Budget.AccountNotMovement", "m", new { accountCode = "X" });
        var r = await EntregaDeInformes.EntregarAsync(Result.Failure<TablaExportable>(error), null, "x");
        var cuerpo = r.Should().BeAssignableTo<IValueHttpResult>().Which.Value!;
        cuerpo.GetType().GetProperty("data").Should().NotBeNull();
        cuerpo.GetType().GetProperty("errorCode")!.GetValue(cuerpo).Should().Be("Accounting.Budget.AccountNotMovement");
    }

    [Fact]
    public async Task UnFormatoDesconocidoSigueSiendo400()
    {
        var r = await EntregaDeInformes.EntregarAsync(Result.Success(Tabla), "csv", "x");
        r.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var cuerpo = r.Should().BeAssignableTo<IValueHttpResult>().Which.Value!;
        cuerpo.GetType().GetProperty("code")!.GetValue(cuerpo).Should().Be("Reportes.FormatoInvalido");
    }

    /// <summary>
    /// <c>[AsParameters]</c> sobre una clase con <c>set</c>: cada filtro de la query string llega a
    /// <see cref="FiltrosDeInforme"/> con su nombre en camelCase. Levanta un host mínimo con una sola
    /// ruta que devuelve lo que entendió; sin base de datos ni identidad.
    /// </summary>
    [Fact]
    public async Task LosFiltrosLleganDesdeLaQueryString()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.MapGet("/f", ([AsParameters] AccountingReportsEndpoints.FiltrosQuery q, string? node) => Results.Ok(new { filtros = q.ToFiltros(), node }));
        await app.StartAsync();
        using var http = app.GetTestClient();

        var persona = Guid.NewGuid();
        var respuesta = await http.GetAsync(
            $"/f?from=2026-01-01&to=2026-03-31&accountFrom=11&accountTo=13&person={persona}&crossDocument=FC|123&voucherType=cg&origin=NOM&user=ana&level=4&withThirdParties=true&includeClosing=true&format=xlsx&node=account:1105");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Respuesta>();
        cuerpo.Should().NotBeNull();
        var f = cuerpo!.Filtros;
        f.From.Should().Be(new DateOnly(2026, 1, 1));
        f.To.Should().Be(new DateOnly(2026, 3, 31));
        f.AccountFrom.Should().Be("11");
        f.AccountTo.Should().Be("13");
        f.Person.Should().Be(persona);
        f.Cruce().Should().Be(("FC", "123"));
        f.VoucherType.Should().Be("cg");
        f.Origin.Should().Be("NOM");
        f.User.Should().Be("ana");
        f.Level.Should().Be(4);
        f.WithThirdParties.Should().BeTrue();
        f.IncludeClosing.Should().BeTrue();
        f.Format.Should().Be("xlsx");
        f.EsExportacion.Should().BeTrue();
        cuerpo.Node.Should().Be("account:1105");

        // Sin nada en la query string, todo nulo y las banderas apagadas (no 400 por parámetros ausentes).
        var vacia = await http.GetFromJsonAsync<Respuesta>("/f");
        vacia!.Filtros.From.Should().BeNull();
        vacia.Filtros.WithThirdParties.Should().BeFalse();
        vacia.Filtros.EsExportacion.Should().BeFalse();
        vacia.Node.Should().BeNull();

        await app.StopAsync();
    }

    private sealed record Respuesta(FiltrosDeInforme Filtros, string? Node);
}
