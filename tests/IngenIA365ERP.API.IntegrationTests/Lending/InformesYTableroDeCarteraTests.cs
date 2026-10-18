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
/// La parte K5 (tablero y vistas de K5) es <see cref="K5_calidad_cosechas_concentracion_documentos_y_tablero_por_oficina"/> (T0979).
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class InformesYTableroDeCarteraTests(CentralIdentityApiFixture fx)
{
    private const string Informes = "/api/reports/lending";

    private static readonly string[] VistasDeK1 = ["aging", "placement", "receipts", "transactions", "usury", "debtor-messages", "migration-reconciliation"];
    private static readonly string[] ConDatosDelDeudor = ["aging", "transactions", "usury", "debtor-messages", "migration-reconciliation"];

    /// <summary>K2 (fase 15, T0850): las dos vistas de la libranza, publicadas desde que la entrega vigente es K2; la del cuadre trae datos del deudor.</summary>
    private static readonly string[] VistasDeK2 = ["payroll-agreements", "payroll-deduction-reconciliation"];

    /// <summary>K3: los reestructurados (fase 16, T0890) y la cartera castigada (fase 17, T0915), con datos del deudor.</summary>
    private static readonly string[] VistasDeK3 = ["restructured", "charge-offs"];

    /// <summary>K4 (fase 18, T0953): las cuatro vistas de la cobranza, con datos del deudor.</summary>
    private static readonly string[] VistasDeK4 = ["collection-stages", "promises", "payment-agreements", "prior-notices"];

    /// <summary>
    /// K5 (fase 20, T0982): calidad y cosechas (agregadas), concentración (con datos del deudor sólo por deudor y por grupo) y documentos emitidos;
    /// fase 21 (T1006): los excluidos del archivo de una central.
    /// </summary>
    private static readonly string[] VistasDeK5 = ["quality", "vintages", "concentration", "issued-documents", "bureau-exclusions"];

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
        registro.Select(v => v.GetProperty("key").GetString()).Should()
            .BeEquivalentTo(VistasDeK1.Concat(VistasDeK2).Concat(VistasDeK3).Concat(VistasDeK4).Concat(VistasDeK5));
        registro.Where(v => v.GetProperty("personalData").GetBoolean()).Select(v => v.GetProperty("key").GetString())
            .Should().BeEquivalentTo(ConDatosDelDeudor.Append("payroll-deduction-reconciliation").Concat(VistasDeK3).Concat(VistasDeK4)
                .Concat(["concentration", "issued-documents", "bureau-exclusions"]));
        registro.Where(v => VistasDeK1.Contains(v.GetProperty("key").GetString())).Should().OnlyContain(v => v.GetProperty("availableFrom").GetInt32() == 1);
        registro.Where(v => VistasDeK2.Contains(v.GetProperty("key").GetString())).Should().OnlyContain(v => v.GetProperty("availableFrom").GetInt32() == 2);
        registro.Where(v => VistasDeK3.Contains(v.GetProperty("key").GetString())).Should().OnlyContain(v => v.GetProperty("availableFrom").GetInt32() == 3);
        registro.Where(v => VistasDeK4.Contains(v.GetProperty("key").GetString())).Should().OnlyContain(v => v.GetProperty("availableFrom").GetInt32() == 4);
        registro.Where(v => VistasDeK5.Contains(v.GetProperty("key").GetString())).Should().OnlyContain(v => v.GetProperty("availableFrom").GetInt32() == 5);
        var delOficial = (await CarteraE2E.GetAsync(http, oficial, Informes)).EnumerateArray().Select(v => v.GetProperty("key").GetString()).ToList();
        delOficial.Should().BeEquivalentTo(["placement", "receipts", "payroll-agreements", "quality", "vintages", "concentration"],
            "sin Lending.Loans.Read no se ofrecen las vistas con datos del deudor; la concentración sí, porque por línea, sector y pagaduría es agregada");
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

    /// <summary>
    /// T0979 (fase 20, K5; FR-133–FR-135, FR-138; US15-1, US15-2), en el motor de <c>DB_PROVIDER</c>, sobre la cooperativa aislada «informes_k5»:
    /// <list type="number">
    /// <item><c>concentration</c> por deudor señala al deudor cerca del límite individual con su porcentaje (US15-2); sin <c>Lending.Loans.Read</c>,
    /// por deudor y por grupo es el 404 genérico y por línea responde; exportar por deudor sin el permiso de datos personales es el 404 y por sector
    /// sale;</item>
    /// <item><c>issued-documents</c> lista el plan de pagos emitido con su estado a hoy;</item>
    /// <item><c>quality</c> y el deterioro del tablero salen de un cierre del mes sembrado como hecho (el cierre real lo recorre
    /// <c>CierreMensualDeCarteraTests</c>); <c>vintages</c> responde con la cosecha del mes (seis meses de reloj con cierres reales no caben en la
    /// suite: la prueba independiente de la cosecha es <c>CalidadCosechasYConcentracionTests</c>);</item>
    /// <item>el tablero de una asesora de O1 trae su crédito y el de una asesora de O2 no lo ve (US15-1), ni pidiendo la oficina de la otra.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task K5_calidad_cosechas_concentracion_documentos_y_tablero_por_oficina()
    {
        var esc = await EscenarioAsync(fx, "informes_k5");
        var coop = esc.Coop;
        var admin = coop.TokenAdmin;
        var u = esc.Usuarios;
        using var http = fx.CreateClient();

        var (_, persona, desembolso) = await CreditoDesembolsadoAsync(http, esc, "Concentrada", 1_000_000m);
        var credito = desembolso.GetProperty("loanPublicId").GetGuid();

        var verInformes = await RolPropioAsync(http, admin, "ENS-INF5-VER", "Lending.Reports.View");
        var verTablero = await RolPropioAsync(http, admin, "ENS-TAB-VER", "Lending.Dashboard.View");
        await DarRolAsync(http, admin, u.Oficial, verInformes);
        foreach (var asesora in new[] { u.AsesorO1, u.AsesorO2 }) await DarRolAsync(http, admin, asesora, verTablero);
        var oficial = await UsuariosDeCartera.IngresarAsync(http, u.Oficial);
        var asesoraO1 = await UsuariosDeCartera.IngresarAsync(http, u.AsesorO1);
        var asesoraO2 = await UsuariosDeCartera.IngresarAsync(http, u.AsesorO2);
        var jefe = await UsuariosDeCartera.IngresarAsync(http, u.Jefe);
        var contadora = await UsuariosDeCartera.IngresarAsync(http, u.Contadora);
        var administrador = await UsuariosDeCartera.IngresarAsync(http, u.Administrador);

        // (1) Concentración: un patrimonio técnico con el que el millón del deudor es el 85 % del límite individual (10 %).
        fx.Dobles.PatrimonioTecnico(coop.TenantPublicId, 1_000_000m / 0.085m, esc.Hoy.AddDays(-1));
        var concentracion = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, contadora, $"{Informes}/concentration"));
        concentracion.Columnas.Should().ContainInOrder("Dimensión", "Saldo", "Participación", "% del límite individual", "Señal");
        var deudor = concentracion.Filas.Should()
            .ContainSingle(f => ContabilidadE2E.Tabla.Celda(f, concentracion.IndicePorClave("_persona")).GetString() == persona.ToString()).Subject;
        ContabilidadE2E.Tabla.Numero(deudor, concentracion.Indice("% del límite individual")).Should().BeApproximately(85m, 0.01m);
        ContabilidadE2E.Tabla.Celda(deudor, concentracion.Indice("Señal")).GetString().Should().Be("Cerca del límite", "US15-2");

        (await CarteraE2E.MandarGetAsync(http, oficial, $"{Informes}/concentration")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "por deudor trae datos del deudor y el oficial no tiene Lending.Loans.Read");
        (await CarteraE2E.MandarGetAsync(http, oficial, $"{Informes}/concentration?by=group")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CarteraE2E.MandarGetAsync(http, oficial, $"{Informes}/concentration?by=line")).StatusCode.Should().Be(HttpStatusCode.OK,
            "por línea es agregada: basta Lending.Reports.View");
        var porLinea = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, contadora, $"{Informes}/concentration?by=line"));
        porLinea.Filas.Should().NotBeEmpty();
        (await CarteraE2E.MandarGetAsync(http, jefe, $"{Informes}/concentration?format=xlsx")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "exportar por deudor exige Lending.Reports.ExportPersonalData");
        (await CarteraE2E.MandarGetAsync(http, jefe, $"{Informes}/concentration?by=sector&format=xlsx")).StatusCode.Should().Be(HttpStatusCode.OK,
            "por sector es agregada");
        var malo = await CarteraE2E.MandarGetAsync(http, contadora, $"{Informes}/concentration?by=producto");
        (await CarteraE2E.CodigoDeErrorAsync(malo)).Should().Be("Lending.Report.FilterInvalid");

        // (2) Documentos emitidos: el plan de pagos del crédito.
        await DocumentosE2E.FirmanteAsync(fx, coop.TenantPublicId, admin, esc.Hoy);
        var plan = await DocumentosE2E.EmitidoAsync(http, administrador, DocumentosE2E.Pedido("PaymentPlan", persona, credito));
        var documentos = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, contadora, $"{Informes}/issued-documents?kind=PaymentPlan"));
        documentos.Columnas.Should().ContainInOrder("Fecha", "Clase", "Número", "Titular", "Estado", "Código", "Emitido por");
        var fila = documentos.Filas.Should().ContainSingle().Subject;
        ContabilidadE2E.Tabla.Celda(fila, documentos.IndicePorClave("_documento")).GetString()
            .Should().Be(plan.GetProperty("documentPublicId").GetGuid().ToString());
        ContabilidadE2E.Tabla.Celda(fila, documentos.Indice("Clase")).GetString().Should().Be("Plan de pagos");
        ContabilidadE2E.Tabla.Celda(fila, documentos.Indice("Estado")).GetString().Should().Be("Vigente");
        (await CarteraE2E.MandarGetAsync(http, oficial, $"{Informes}/issued-documents")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // (3) Calidad y cosechas: un cierre del mes sembrado como hecho, con el crédito en B a 45 días y su deterioro.
        await CierreSembradoAsync(esc, credito);
        var calidad = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, contadora, $"{Informes}/quality?groupBy=office"));
        calidad.Columnas.Should().ContainInOrder("Dimensión", "Cartera bruta", "Vencida", "Vencida/bruta", "En riesgo (B–E)", "Riesgo/bruta", "Deterioro", "Cobertura");
        ContabilidadE2E.Tabla.Numero(calidad.Totales!.Value, calidad.Indice("Cartera bruta")).Should().Be(1_000_000m);
        ContabilidadE2E.Tabla.Numero(calidad.Totales!.Value, calidad.Indice("Vencida")).Should().Be(1_000_000m, "45 días son más de 30");
        ContabilidadE2E.Tabla.Numero(calidad.Totales!.Value, calidad.Indice("Deterioro")).Should().Be(10_000m);
        ContabilidadE2E.Tabla.Numero(calidad.Totales!.Value, calidad.Indice("Cobertura")).Should().Be(1m);
        var cosechas = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, contadora, $"{Informes}/vintages"));
        cosechas.Columnas.Should().ContainInOrder("Mes de desembolso", "Monto", "Mes de maduración", "Mes del cierre", "Mora 30", "Mora 60", "Mora 90");
        var cosecha = cosechas.Filas.Should().ContainSingle("la cosecha del mes todavía no tiene cierres posteriores").Subject;
        ContabilidadE2E.Tabla.Celda(cosecha, cosechas.Indice("Mes de desembolso")).GetString().Should().Be(esc.Hoy.ToString("yyyy-MM"));
        ContabilidadE2E.Tabla.Numero(cosecha, cosechas.Indice("Monto")).Should().Be(1_000_000m);

        // (4) El tablero: la asesora de O1 ve el crédito de su oficina; la de O2, no (US15-1).
        var deO1 = await CarteraE2E.GetAsync(http, asesoraO1, "/api/lending/dashboard");
        var deO2 = await CarteraE2E.GetAsync(http, asesoraO2, "/api/lending/dashboard");
        static decimal? Valor(JsonElement t, string clave)
        {
            var v = t.GetProperty("tiles").EnumerateArray().Single(f => f.GetProperty("key").GetString() == clave).GetProperty("value");
            return v.ValueKind == JsonValueKind.Null ? null : v.GetDecimal();
        }
        deO1.GetProperty("tiles").EnumerateArray().Select(f => f.GetProperty("key").GetString()).Should().Equal("saldo", "colocacion-del-mes", "calidad",
            "deterioro", "recaudo-vs-esperado", "cosechas", "cumplimiento-de-promesas", "mensajes-pendientes-y-rechazados", "alertas-pendientes");
        Valor(deO1, "saldo").Should().Be(1_000_000m);
        Valor(deO1, "colocacion-del-mes").Should().Be(1_000_000m);
        Valor(deO1, "deterioro").Should().Be(10_000m);
        Valor(deO2, "saldo").Should().Be(0m, "la asesora de O2 no ve lo de O1");
        Valor(deO2, "colocacion-del-mes").Should().Be(0m);
        deO2.GetProperty("scope").GetProperty("branches").EnumerateArray().Select(b => b.GetProperty("publicId").GetGuid())
            .Should().NotContain(u.O1.AgenciaPublicId);
        Valor(await CarteraE2E.GetAsync(http, asesoraO2, $"/api/lending/dashboard?branch={u.O1.AgenciaPublicId}"), "saldo").Should().Be(0m,
            "pedir la oficina de otra no la trae");
        deO1.GetRawText().Should().NotContain(persona.ToString()).And.NotContain(credito.ToString());
    }

    /// <summary>
    /// Un cierre mensual del mes del reloj sembrado en la base como hecho (sin corrida: sus líneas van por el cierre), con el crédito en B, 45 días de
    /// mora, su capital de 1.000.000 y un deterioro de 10.000. Lo hace la prueba porque un cierre real pide el mes terminado y causado.
    /// </summary>
    private async Task CierreSembradoAsync(DesembolsoE2E.Escenario esc, Guid credito)
    {
        await EnLaBaseAsync(fx, esc, async db =>
        {
            var loan = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Loans, l => l.PublicId == credito);
            var cierre = new IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyClose
            {
                Kind = IngenIA365ERP.Domain.Enums.Lending.MonthlyCloseKind.Monthly, PeriodYear = (short)esc.Hoy.Year, PeriodMonth = (byte)esc.Hoy.Month,
                Version = 1, CloseDate = esc.Hoy, InformationCutoffAt = DateTime.UtcNow, OrderedAt = DateTime.UtcNow, ClosedAt = DateTime.UtcNow,
                LoanCount = 1, TotalPrincipal = 1_000_000m, TotalIndividualImpairment = 10_000m, LinesSha256 = "sembrado-e2e",
            };
            db.MonthlyCloses.Add(cierre);
            await db.SaveChangesAsync(CancellationToken.None);
            db.MonthlyCloseLoans.Add(new IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseLoan
            {
                MonthlyCloseId = cierre.Id, LoanId = loan.Id, PersonId = loan.DebtorPersonId, BranchId = loan.BranchId, CreditLineId = loan.CreditLineId,
                OfficePublicId = Guid.NewGuid(), ModalityCode = "CONSUMO", SarcSegmentCode = "CONSUMO", DaysPastDueAtClose = 45, MaxDaysPastDueInMonth = 45,
                ArrearsRating = IngenIA365ERP.Domain.Enums.Lending.RatingCategory.B, FinalRating = IngenIA365ERP.Domain.Enums.Lending.RatingCategory.B,
                AccountingRating = IngenIA365ERP.Domain.Enums.Lending.RatingCategory.B, PrincipalBalance = 1_000_000m, TotalImpairment = 10_000m,
            });
            await db.SaveChangesAsync(CancellationToken.None);
            return 0;
        });
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
