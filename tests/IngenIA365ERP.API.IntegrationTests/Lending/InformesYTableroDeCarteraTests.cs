using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.OriginacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0774 (feature 013, US15; FR-133, FR-138; contracts/api.md §17, §1.10; C84, C109), parte K1, en el motor de <c>DB_PROVIDER</c>, sobre una
/// cooperativa aislada activada («informes»: el escenario del desembolso, con un crédito desembolsado en la oficina O1):
/// <list type="number">
/// <item>el registro <c>GET /api/reports/lending</c> publica las siete vistas de K1 y, a quien no tiene <c>Lending.Loans.Read</c>, sin las de
/// datos del deudor (que además le responden el 404 genérico);</item>
/// <item><c>aging</c> en json y en xlsx: sin <c>Lending.Reports.ExportPersonalData</c> el archivo es el mismo 404 y no se audita; con él, sale y
/// queda un evento <c>Lending.Report.Exported</c> con la vista y las filas en la base de auditoría de la cooperativa;</item>
/// <item>un usuario de una oficina sólo ve lo suyo en <c>placement</c> y <c>receipts</c>;</item>
/// <item><c>debtor-messages</c> y <c>migration-reconciliation</c> responden con sus columnas.</item>
/// </list>
/// La parte K5 (tablero, vistas de K5) la suma la fase de US15 (K5).
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class InformesYTableroDeCarteraTests(CentralIdentityApiFixture fx)
{
    private const string Informes = "/api/reports/lending";

    private static readonly string[] VistasDeK1 = ["aging", "placement", "receipts", "transactions", "usury", "debtor-messages", "migration-reconciliation"];
    private static readonly string[] ConDatosDelDeudor = ["aging", "transactions", "usury", "debtor-messages", "migration-reconciliation"];

    /// <summary>K2 (fase 15, T0850): las dos vistas de la libranza, publicadas desde que la entrega vigente es K2; la del cuadre trae datos del deudor.</summary>
    private static readonly string[] VistasDeK2 = ["payroll-agreements", "payroll-deduction-reconciliation"];

    [Fact]
    public async Task K1_registro_permisos_exportacion_auditada_alcance_y_columnas()
    {
        var esc = await EscenarioAsync(fx, "informes");
        var coop = esc.Coop;
        var admin = coop.TokenAdmin;
        var u = esc.Usuarios;
        using var http = fx.CreateClient();
        var desde = DateTime.UtcNow.AddMinutes(-1);

        // Un crédito desembolsado en O1 y un pago del recaudador de prueba.
        var (_, persona, desembolso) = await CreditoDesembolsadoAsync(http, esc, "Informe");
        var credito = desembolso.GetProperty("loanPublicId").GetGuid();

        // Los permisos del ensayo: el oficial sólo ve informes (sin Loans.Read); las dos asesoras ven informes con su oficina y sus líneas.
        var verInformes = await RolPropioAsync(http, admin, "ENS-INF-VER", "Lending.Reports.View");
        await DarRolAsync(http, admin, u.Oficial, verInformes);
        await DarRolAsync(http, admin, u.AsesorO1, verInformes);
        await DarRolAsync(http, admin, u.AsesorO2, verInformes);
        var lineas = (await CarteraE2E.GetAsync(http, admin, "/api/lending/credit-lines")).EnumerateArray().Select(l => l.GetProperty("publicId").GetGuid()).ToArray();
        await UsuariosDeCartera.FijarLineasAsync(http, admin, u.AsesorO2, lineas);
        var oficial = await UsuariosDeCartera.IngresarAsync(http, u.Oficial);
        var asesoraO1 = await UsuariosDeCartera.IngresarAsync(http, u.AsesorO1);
        var asesoraO2 = await UsuariosDeCartera.IngresarAsync(http, u.AsesorO2);
        var jefe = await UsuariosDeCartera.IngresarAsync(http, u.Jefe);
        var contadora = await UsuariosDeCartera.IngresarAsync(http, u.Contadora);

        // (1) El registro: las siete de K1 para quien lee créditos; sin las de datos del deudor para quien no.
        var registro = (await CarteraE2E.GetAsync(http, contadora, Informes)).EnumerateArray().ToList();
        registro.Select(v => v.GetProperty("key").GetString()).Should().BeEquivalentTo(VistasDeK1.Concat(VistasDeK2));
        registro.Where(v => v.GetProperty("personalData").GetBoolean()).Select(v => v.GetProperty("key").GetString())
            .Should().BeEquivalentTo(ConDatosDelDeudor.Append("payroll-deduction-reconciliation"));
        registro.Where(v => VistasDeK1.Contains(v.GetProperty("key").GetString())).Should().OnlyContain(v => v.GetProperty("availableFrom").GetInt32() == 1);
        registro.Where(v => VistasDeK2.Contains(v.GetProperty("key").GetString())).Should().OnlyContain(v => v.GetProperty("availableFrom").GetInt32() == 2);
        var delOficial = (await CarteraE2E.GetAsync(http, oficial, Informes)).EnumerateArray().Select(v => v.GetProperty("key").GetString()).ToList();
        delOficial.Should().BeEquivalentTo(["placement", "receipts", "payroll-agreements"], "sin Lending.Loans.Read no se ofrecen las vistas con datos del deudor");
        foreach (var vista in ConDatosDelDeudor)
            (await CarteraE2E.MandarGetAsync(http, oficial, $"{Informes}/{vista}")).StatusCode.Should().Be(HttpStatusCode.NotFound, $"{vista} sin Loans.Read");
        (await CarteraE2E.MandarGetAsync(http, oficial, $"{Informes}/placement")).StatusCode.Should().Be(HttpStatusCode.OK);

        // (2) aging en json, y en xlsx sin y con ExportPersonalData.
        var aging = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, jefe, $"{Informes}/aging"));
        aging.Columnas.Should().ContainInOrder("Crédito", "Deudor", "Línea", "Modalidad", "Oficina", "Saldo de capital", "Días de mora", "Rango", "Calificación");
        var fila = aging.Filas.Should().ContainSingle(f => ContabilidadE2E.Tabla.Celda(f, aging.IndicePorClave("_credito")).GetString() == credito.ToString()).Subject;
        ContabilidadE2E.Tabla.Celda(fila, aging.IndicePorClave("_persona")).GetString().Should().Be(persona.ToString());
        ContabilidadE2E.Tabla.Numero(fila, aging.Indice("Saldo de capital")).Should().BePositive();

        var sinPermiso = await CarteraE2E.MandarGetAsync(http, jefe, $"{Informes}/aging?format=xlsx");
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound, "el jefe exporta pero no tiene Lending.Reports.ExportPersonalData (C84)");
        (await CarteraE2E.CodigoDeErrorAsync(sinPermiso)).Should().Be("Generic.NotFound");
        (await CarteraE2E.MandarGetAsync(http, jefe, $"{Informes}/placement?format=xlsx")).StatusCode.Should().Be(HttpStatusCode.OK, "placement es agregada");
        await AuditadasAsync(coop, desde, "aging", 0, "sin el permiso no sale nada ni se audita");

        var archivo = await CarteraE2E.MandarGetAsync(http, contadora, $"{Informes}/aging?format=xlsx");
        archivo.StatusCode.Should().Be(HttpStatusCode.OK, await archivo.Content.ReadAsStringAsync());
        archivo.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await archivo.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
        var exportaciones = await AuditadasAsync(coop, desde, "aging", 1, "cada exportación a archivo deja su evento en la cooperativa");
        exportaciones.Single().GetRawText().Should().Contain("filas");

        // (3) Una oficina: la asesora de O1 ve la colocación y el recaudo del crédito; la de O2, no.
        await PagosE2E.AplicadoAsync(http, u, PagosE2E.Pago(credito, persona, "FreeAmount", 50_000m, esc.Hoy));
        foreach (var (vista, columna) in new[] { ("placement", "Monto desembolsado"), ("receipts", "Total") })
        {
            var deO1 = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, asesoraO1, $"{Informes}/{vista}"));
            var deO2 = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, asesoraO2, $"{Informes}/{vista}"));
            deO1.Filas.Should().NotBeEmpty($"{vista}: el crédito es de O1");
            deO2.Filas.Should().BeEmpty($"{vista}: la asesora de O2 no ve lo de O1");
            ContabilidadE2E.Tabla.Numero(deO1.Totales!.Value, deO1.Indice(columna)).Should().BePositive();
        }
        var porOficina = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, asesoraO2, $"{Informes}/placement?branch={u.O1.AgenciaPublicId}"));
        porOficina.Filas.Should().BeEmpty("pedir la sucursal de otra oficina no la trae");

        // (4) Mensajes al deudor y conciliación de la migración responden con sus columnas.
        var mensajes = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, contadora, $"{Informes}/debtor-messages"));
        mensajes.Columnas.Should().ContainInOrder("Fecha", "Crédito", "Destinatario", "Canal", "Propósito", "Plantilla", "Estado", "Motivo de exclusión o diferimiento");
        mensajes.Claves.Should().Contain(["_credito", "_mensaje"]);
        var activacion = await CarteraE2E.ActivarCarteraAsync(fx, coop);
        var migracion = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, contadora, $"{Informes}/migration-reconciliation?load={activacion.MigrationLoadPublicId}"));
        migracion.Columnas.Should().ContainInOrder("Concepto", "Modalidad", "Sub-segmento", "Categoría", "Conjunto de cuentas", "Cartera", "Contabilidad",
            "Diferencia", "Factura", "Cliente", "Clasificación", "Saldo", "Aceptación", "Punto de acta");
        var sinCarga = await CarteraE2E.MandarGetAsync(http, contadora, $"{Informes}/migration-reconciliation");
        sinCarga.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(sinCarga)).Should().Be("Lending.Report.FilterInvalid");

        // Los filtros comunes: rango al revés y filtro propio fuera de su lista.
        var alReves = await CarteraE2E.MandarGetAsync(http, jefe, $"{Informes}/placement?from={esc.Hoy:yyyy-MM-dd}&to={esc.Hoy.AddDays(-5):yyyy-MM-dd}");
        alReves.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(alReves)).Should().Be("Lending.Report.RangeInvalid");
        var largo = await CarteraE2E.MandarGetAsync(http, jefe, $"{Informes}/placement?from={esc.Hoy.AddYears(-6):yyyy-MM-dd}&to={esc.Hoy:yyyy-MM-dd}");
        (await CarteraE2E.CodigoDeErrorAsync(largo)).Should().Be("Lending.Report.RangeTooLong");
        var porProducto = await CarteraE2E.MandarGetAsync(http, jefe, $"{Informes}/placement?groupBy=producto");
        porProducto.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(porProducto)).Should().Be("Lending.Report.FilterInvalid");
    }

    /// <summary>Los eventos <c>Lending.Report.Exported</c> de una vista en la base de auditoría de la cooperativa, con el número esperado.</summary>
    private async Task<List<JsonElement>> AuditadasAsync(CarteraE2E.CooperativaAislada coop, DateTime desde, string vista, int esperadas, string porque)
    {
        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);
        await fx.Factory.Services.GetRequiredService<IAuditService>().FlushAsync();
        using var http = fx.CreateClient();
        var hasta = DateTime.UtcNow.AddMinutes(1);
        var lote = await CarteraE2E.GetAsync(http, coop.TokenAdmin,
            $"/api/audit/logs?action={AuditEventTypes.LendingReportExported}&from={desde:O}&to={hasta:O}&pageSize=100");
        var eventos = lote.GetProperty("items").EnumerateArray().Where(e => e.GetRawText().Contains($"\\u0022{vista}\\u0022", StringComparison.Ordinal)
                                                                         || e.GetRawText().Contains($"\"{vista}\"", StringComparison.Ordinal)
                                                                         || e.GetRawText().Contains($"\\\"{vista}\\\"", StringComparison.Ordinal)).ToList();
        eventos.Should().HaveCount(esperadas, porque);
        return eventos;
    }
}
