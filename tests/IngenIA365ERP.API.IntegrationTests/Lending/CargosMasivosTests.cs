using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Feature 026 (2026-11-25; T049): los cargos masivos de punta a punta en una cooperativa aislada con Cartera activa y sin contabilidad iniciada
/// (los hechos no pasan: lo contable lo cubren los casos dorados 66 y 67). La línea marcada, tres asociados, la vista previa, la confirmación, la
/// corrida de fondo conducida tanda por tanda, las obligaciones creadas con su origen, la unicidad del período al repetir y la anulación de un cargo.
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CargosMasivosTests(CentralIdentityApiFixture fx)
{
    private const string Cargos = "/api/lending/bulk-charges";

    [Fact]
    public async Task Una_corrida_carga_a_los_asociados_activos_no_duplica_el_periodo_y_anula_un_cargo_sin_movimientos()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "cargos-masivos");
        await CarteraE2E.ActivarCarteraAsync(fx, coop);
        await LineasE2E.PrepararAsync(fx, coop);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;

        // La línea marcada: FDOSOL del ensayo, sin interés, una cuota de 13.557 por defecto, caja.
        var version = LineasE2E.VersionFija(hoy, "CONSUMO", "SinLibranza", 0m, "Fondo de solidaridad (e2e de cargos masivos)");
        version["amounts"] = new { min = 1_000m, max = 100_000m };
        version["terms"] = new { minMonths = 1, maxMonths = 12 };
        version["allowsBulkCharge"] = true;
        version["bulkChargeDefaultAmount"] = 13_557m;
        version["bulkChargeDefaultInstallments"] = 1;
        var linea = await LineasE2E.CrearExitoAsync(http, admin, "FDOSOLE2E", "Fondo de solidaridad", version);

        var asociados = new List<Guid>();
        foreach (var nombre in new[] { "Amparo", "Benito", "Celmira" })
            asociados.Add(await OriginacionE2E.AsociadoAsync(http, admin, nombre));

        var periodo = hoy.Year * 100 + hoy.Month;
        object Cuerpo() => new
        {
            creditLinePublicId = linea, period = periodo, chargeDate = hoy, firstDueDate = hoy.AddDays(30),
            channelMode = "Fixed", repaymentChannel = "Cashier", counterpartMode = "Debtor", source = "Filters",
        };

        // La vista previa no crea nada y trae a los tres.
        var creada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cargos, Cuerpo());
        creada.StatusCode.Should().Be(HttpStatusCode.Created, await creada.Content.ReadAsStringAsync());
        var corrida = await CarteraE2E.LeerAsync(creada);
        var id = corrida.GetProperty("publicId").GetGuid();
        corrida.GetProperty("status").GetInt32().Should().Be((int)BulkChargeRunStatus.Draft);
        corrida.GetProperty("amount").GetDecimal().Should().Be(13_557m, "el valor por defecto de la línea");
        corrida.GetProperty("pendingCount").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        var lineas = await LineasAsync(http, admin, id);
        lineas.Where(l => asociados.Contains(l.Persona)).Should().HaveCount(3).And.OnlyContain(l => l.Estado == (int)BulkChargeLineStatus.Pending);
        corrida.GetProperty("canConfirm").GetBoolean().Should().BeTrue();

        // Una huella vieja no confirma.
        var vieja = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cargos}/{id}/confirm", new { expectedPreviewSha256 = new string('0', 64) });
        (await CarteraE2E.CodigoDeErrorAsync(vieja)).Should().Be("Lending.BulkCharge.PreviewChanged");

        // Confirmada, queda en cola y la corrida de fondo crea las obligaciones.
        var confirmada = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Cargos}/{id}/confirm",
            new { expectedPreviewSha256 = corrida.GetProperty("previewSha256").GetString() });
        confirmada.GetProperty("status").GetInt32().Should().Be((int)BulkChargeRunStatus.Queued);
        await ConducirAsync(coop.TenantPublicId, confirmada.GetProperty("processRunPublicId").GetGuid(), LendingProcessKind.BulkCharge);

        var terminada = await CarteraE2E.GetAsync(http, admin, $"{Cargos}/{id}");
        terminada.GetProperty("status").GetInt32().Should().Be((int)BulkChargeRunStatus.Completed);
        terminada.GetProperty("failedCount").GetInt32().Should().Be(0);
        terminada.GetProperty("createdCount").GetInt32().Should().Be(corrida.GetProperty("pendingCount").GetInt32());
        lineas = await LineasAsync(http, admin, id);
        var nuestras = lineas.Where(l => asociados.Contains(l.Persona)).ToList();
        nuestras.Should().HaveCount(3).And.OnlyContain(l => l.Estado == (int)BulkChargeLineStatus.Created && l.Credito != null);

        // La obligación es un crédito de la línea, de origen «Cargo masivo», por el valor del cargo.
        var credito = await CarteraE2E.GetAsync(http, admin, $"/api/lending/loans/{nuestras[0].Credito}");
        credito.GetProperty("origin").ToString().Should().BeOneOf("BulkCharge", ((int)LoanOrigin.BulkCharge).ToString());
        credito.GetProperty("approvedAmount").GetDecimal().Should().Be(13_557m);

        // Repetir el período no vuelve a cargar a nadie.
        var otra = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cargos, Cuerpo());
        otra.StatusCode.Should().Be(HttpStatusCode.Created, await otra.Content.ReadAsStringAsync());
        var otraId = (await CarteraE2E.LeerAsync(otra)).GetProperty("publicId").GetGuid();
        (await LineasAsync(http, admin, otraId)).Where(l => asociados.Contains(l.Persona))
            .Should().HaveCount(3).And.OnlyContain(l => l.Estado == (int)BulkChargeLineStatus.Skipped && l.Motivo == "AlreadyCharged");
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Cargos}/{otraId}/discard", null);

        // Anular el cargo de uno (sin movimientos) lo deja anulado y libera su período.
        var anulada = nuestras[0];
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Cargos}/{id}/lines/{anulada.Linea}/void", new { reason = "Asociado cargado por error (e2e)" });
        (await LineasAsync(http, admin, id)).Single(l => l.Linea == anulada.Linea).Estado.Should().Be((int)BulkChargeLineStatus.Voided);
        var creditoAnulado = await CarteraE2E.GetAsync(http, admin, $"/api/lending/loans/{anulada.Credito}");
        creditoAnulado.GetProperty("status").ToString().Should().BeOneOf("Voided", ((int)LoanStatus.Voided).ToString());

        var tercera = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cargos, Cuerpo());
        var terceraId = (await CarteraE2E.LeerAsync(tercera)).GetProperty("publicId").GetGuid();
        (await LineasAsync(http, admin, terceraId)).Single(l => l.Persona == anulada.Persona).Estado
            .Should().Be((int)BulkChargeLineStatus.Pending, "anular libera el período");
    }

    private sealed record LineaLeida(Guid Linea, Guid Persona, int Estado, string? Motivo, Guid? Credito);

    private static async Task<List<LineaLeida>> LineasAsync(HttpClient http, string token, Guid corrida)
    {
        var pagina = await CarteraE2E.GetAsync(http, token, $"{Cargos}/{corrida}/lines?pageSize=500");
        return pagina.GetProperty("items").EnumerateArray().Select(l => new LineaLeida(
            l.GetProperty("publicId").GetGuid(),
            l.GetProperty("personPublicId").GetGuid(),
            l.GetProperty("status").GetInt32(),
            l.GetProperty("reason").ValueKind == JsonValueKind.String ? l.GetProperty("reason").GetString() : null,
            l.GetProperty("loanPublicId").ValueKind == JsonValueKind.String ? l.GetProperty("loanPublicId").GetGuid() : null)).ToList();
    }

    /// <summary>Conduce la corrida de fondo como lo hace la pasada de Cartera: tanda por tanda, con el actor del proceso, hasta terminar.</summary>
    private async Task ConducirAsync(Guid tenantPublicId, Guid corridaPublicId, LendingProcessKind clase)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var cooperativa = (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(c => c.PublicId == tenantPublicId);
        var origen = PasadaDeCartera.Origen;
        var ejecutada = await fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>().EjecutarAsync(cooperativa,
            Actor.ProcesoDeIntegracion(origen), origen, async (servicios, ct) =>
            {
                var db = servicios.GetRequiredService<IApplicationDbContext>();
                var corrida = await db.LendingProcessRuns.FirstAsync(r => r.PublicId == corridaPublicId, ct);
                var ejecutor = servicios.GetServices<IEjecutorDeCorridaDeCartera>().Single(e => e.Clase == clase);
                corrida.Status = LendingProcessRunStatus.Running;
                await db.SaveChangesAsync(ct);
                for (var i = 0; i < 1_000; i++)
                {
                    var avance = await ejecutor.EjecutarTandaAsync(corrida, TimeSpan.FromMinutes(5), ct);
                    corrida.BatchCount += 1;
                    if (avance == AvanceDeCorrida.Terminada)
                    {
                        corrida.Status = LendingProcessRunStatus.Completed;
                        await db.SaveChangesAsync(ct);
                        return;
                    }
                    await db.SaveChangesAsync(ct);
                }
                throw new InvalidOperationException("La corrida de cargos masivos no terminó en 1.000 tandas.");
            }, CancellationToken.None);
        ejecutada.Should().Be(ResultadoEnCooperativa.Ejecutada);
    }
}
