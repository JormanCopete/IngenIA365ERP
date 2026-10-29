using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Application.Lending.Imports;
using Xunit.Abstractions;
using static IngenIA365ERP.API.IntegrationTests.Lending.MigracionE2E;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0729 (feature 013, US8; quickstart §3.3, §3.4; SC-013; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): la migración de la
/// cartera vigente y la activación de Cartera por sus rutas, en una cooperativa aislada con la contabilidad iniciada, el escenario de ventas de la
/// 012 (el POS y el medio <c>CREDASOC</c>), los usuarios del ensayo de Cartera y los dobles de Recaudo y Tesorería. El corte C es hoy (la línea
/// <c>MIGCON</c> nace hoy y la carga exige su versión vigente al corte) y la activación se registra con el reloj de la suite en C+1.
/// <list type="bullet">
/// <item>Cuatro ventas a crédito confirmadas antes del corte (100.000, 200.000, 300.000 y 400.000) esperan a Cartera sin consumirse; una carga de
/// ensayo de un crédito, aplicada, las lista como no migradas y da sus facturas (y queda <c>Replaced</c> cuando se aplica la carga buena, US8-9).</item>
/// <item>La carga de 100 créditos por partes: la revisión de <c>loans</c> con un deudor inexistente lo marca con fila y columna sin guardar nada
/// (US8-1); las cuotas llegan en <b>dos archivos</b>; una con el plan descuadrado hace que aplicar responda <c>CrossPartErrors</c> con parte, fila y
/// columna; <c>replace=true</c> deja las dos tandas <c>Superseded</c> y las nuevas cuadran.</item>
/// <item>Aplicar sólo guarda; el cuadre trae la diferencia de capital contra Contabilidad (US8-2, sin documentos ni nombres); la conciliación da
/// migrada, cancelada antes del corte y no migradas con saldo, y una de servicios se marca <c>OutsideLendingD29</c> desde la lista.</item>
/// <item>Activar con C+2 responde <c>CutoffInvalid</c>; sin aceptar, <c>DifferencesNotAccepted</c> (US8-3); la secretaría (no quien cargó) acepta con
/// el punto del acta; la revisión de cobros provisionales rechaza la factura de una venta hasta el corte; activar con el acta hace nacer los 100
/// créditos y el cierre base, habilita el destino <c>Lending</c> y emite <c>CarteraActivada</c> sin ningún hecho contable de saldo inicial (US8-4);
/// la misma <c>Idempotency-Key</c> devuelve la misma activación.</item>
/// <item>La liquidación a C de un crédito migrado es lo cargado; el despachador entrega las ventas acumuladas y Comercial recibe
/// <c>ObligacionCreada</c> con el número migrado para la migrada y <c>ObligacionRechazada</c> no reprocesable para la no migrada aceptada.</item>
/// </list>
/// Lo que no recorre y fijan las pruebas de Application (anotado en tasks.md, T0729): una venta fechada hasta C que llega después
/// (<c>VentasAnterioresAlCorteTests</c>), las cuotas vencidas entre C+1 y el registro (<c>ActivateLendingCommandHandlerTests</c>) y el primer cierre
/// mensual en delta frente al base (<c>CierreMensualDeCarteraTests</c> lo recorre sobre el base de la activación de prueba). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class MigracionYActivacionDeCarteraTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const string Nombre = "migracion_k1";
    private const int Creditos = 100;

    [Fact]
    public async Task La_cartera_vigente_se_carga_por_partes_cuadra_se_acepta_con_acta_y_se_activa_en_C_mas_1()
    {
        // ------------------------------------------------------------------------------------------ la cooperativa --
        var ventas = await ContabilidadDeVentasE2E.PrepararAsync(fx, Nombre);
        var coop = new CarteraE2E.CooperativaAislada
        {
            TokenAdmin = ventas.Admin, TenantPublicId = ventas.Coop.TenantPublicId, SucursalPrincipal = ventas.Coop.SucursalPrincipal,
            PrimerEjercicioContable = ventas.Coop.PrimerEjercicioContable ?? CarteraE2E.Hoy(fx).Year, CorreoAdmin = $"admin.{Nombre}@coop.inventario.test",
        };
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var tenant = coop.TenantPublicId;
        var corte = CarteraE2E.Hoy(fx);
        await LineaAsync(fx, coop, corte);
        await CarteraE2E.RecaudoYTesoreriaRealesAsync(fx, http, coop, corte);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop);
        var jefe = await UsuariosDeCartera.IngresarAsync(http, usuarios.Jefe);
        var oficina = $"Agencia {usuarios.O1.Codigo}";

        // ------------------------------------------------------------- cuatro ventas a crédito antes del corte --
        var sesion = await EscenarioDeVentas.SesionAsync(http, ventas.Cajero1.Token, ventas.Caja1);
        var clientes = new List<(string Documento, Guid Persona)>();
        var ventasPorMonto = new Dictionary<decimal, Guid>();
        for (var i = 1; i <= 4; i++)
        {
            var documento = Documento();
            var persona = await ContabilidadDeVentasE2E.AsociadoAsync(http, admin, $"Cliente{i}", documento);
            clientes.Add((documento, persona));
            var (venta, cobro) = await VentasConCarteraE2E.CobrarACreditoAsync(http, ventas, ventas.Cajero1.Token, sesion, persona, i,
                VentasConCarteraE2E.Credito(ventas, 100_000m * i));
            cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
            ventasPorMonto[100_000m * i] = venta;
            var cobrada = await InventarioE2E.LeerAsync(cobro);
            if (cobrada.GetProperty("status").GetInt32() == 1)
                await EscenarioDeVentas.AprobadaAsync(await InventarioE2E.DecidirAsync(http, admin, ventas.Supervisor.Token,
                    cobrada.GetProperty("approvalRequestPublicId").GetGuid(), aprobar: true));
        }

        // ----------------------------------------------------------- deudores del ensayo y la carga de ensayo --
        var deudores = new List<string>();
        for (var i = 1; i <= 10; i++) deudores.Add((await PersonaAsync(http, admin, $"Deudor{i:00}")).Documento);
        var (codeudor, _) = await PersonaAsync(http, admin, "Codeudor");

        var ensayo = await CrearCargaAsync(http, jefe, corte, "Carga de ensayo (e2e)");
        await CargarAsync(http, jefe, ensayo, P.Loans, [Credito("EN-0001", deudores[0], oficina, corte.AddDays(-30), 500_000m, 1)]);
        await CargarAsync(http, jefe, ensayo, P.Installments, Cuotas("EN-0001", corte, 1, 500_000m));
        await CargarAsync(http, jefe, ensayo, P.Balances, [Saldo("EN-0001", "Principal", 500_000m)]);
        await CargarAsync(http, jefe, ensayo, P.RiskData, [Riesgo("EN-0001")]);
        var aplicarEnsayo = await AplicarAsync(http, jefe, ensayo, "Ensayo de la carga");
        aplicarEnsayo.StatusCode.Should().Be(HttpStatusCode.OK, await aplicarEnsayo.Content.ReadAsStringAsync());

        // Las ventas acumuladas hasta el corte, sin consumirse: dan las facturas tal como las muestra la venta.
        var pendientes = (await CarteraE2E.GetAsync(http, jefe, $"{Cargas}/{ensayo}/reconciliation?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
        pendientes.Should().HaveCount(4, "las cuatro ventas a crédito hasta el corte esperan a Cartera");
        pendientes.Should().OnlyContain(v => v.GetProperty("classification").GetInt32() == 3, "la carga de ensayo no las migra");
        var factura = pendientes.ToDictionary(v => v.GetProperty("amount").GetDecimal(), v => v.GetProperty("invoiceNumber").GetString()!);
        var (s1, s2, s3, s4) = (factura[100_000m], factura[200_000m], factura[300_000m], factura[400_000m]);

        // --------------------------------------------------------------------- la carga de 100 créditos (US8-1) --
        var carga = await CrearCargaAsync(http, jefe, corte, "Cartera vigente al corte (e2e)");
        var numeros = Enumerable.Range(1, Creditos).Select(i => $"CR-{i:0000}").ToList();
        var creditos = numeros.Select((n, i) => i == 0
            ? Credito(n, clientes[0].Documento, oficina, corte, 100_000m, 10, factura: s1)
            : Credito(n, deudores[i % deudores.Count], oficina, corte.AddDays(-60), 1_200_000m, 12)).ToList();

        var malo = new List<Dictionary<string, string?>>(creditos.Take(2)) { Credito("CR-9999", "99999999", oficina, corte.AddDays(-60), 1_200_000m, 12) };
        var revision = await ImportarAsync(http, jefe, carga, P.Loans, Csv(P.Loans, malo), "review");
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var revisado = await CarteraE2E.LeerAsync(revision);
        revisado.GetProperty("valid").GetBoolean().Should().BeFalse();
        var errorDelDeudor = Errores(revisado).Should().ContainSingle().Subject;
        errorDelDeudor.GetProperty("row").GetInt32().Should().Be(4, "la tercera fila de datos es la fila 4 del archivo");
        errorDelDeudor.GetProperty("column").GetString().Should().Be(C.Deudor);
        errorDelDeudor.GetProperty("code").GetString().Should().Be("Import.Cell.NotFound");
        (await CarteraE2E.GetAsync(http, jefe, $"{Cargas}/{carga}")).GetProperty("parts").GetArrayLength().Should().Be(0, "revisar no guarda nada");

        await CargarAsync(http, jefe, carga, P.Loans, creditos);
        IEnumerable<Dictionary<string, string?>> CuotasDe(IEnumerable<string> lote, bool descuadrar) => lote.SelectMany(n => n == "CR-0001"
            ? Cuotas(n, corte, 10, 10_000m)
            : Cuotas(n, corte, 10, descuadrar && n == numeros[^1] ? 90_000m : 100_000m));
        await CargarAsync(http, jefe, carga, P.Installments, CuotasDe(numeros.Take(50), false), archivo: "cuotas-1.csv");
        await CargarAsync(http, jefe, carga, P.Installments, CuotasDe(numeros.Skip(50), true), archivo: "cuotas-2.csv");
        await CargarAsync(http, jefe, carga, P.Balances, numeros.Select(n => Saldo(n, "Principal", n == "CR-0001" ? 100_000m : 1_000_000m)));
        await CargarAsync(http, jefe, carga, P.RiskData, numeros.Select(Riesgo));
        await CargarAsync(http, jefe, carga, P.CoDebtors, [Fila((C.NumeroDeCredito, "CR-0002"), (C.Codeudor, codeudor))]);
        await CargarAsync(http, jefe, carga, P.Collaterals, [Fila((C.NumeroDeCredito, "CR-0002"), (C.Tipo, "PromissoryNote"), (C.Referencia, "PG-0002"),
            (C.Valor, "1200000"), (C.FechaDeConstitucion, Fecha(corte.AddDays(-60))), (C.ClasePagare, "Physical"))]);
        await CargarAsync(http, jefe, carga, P.Sales,
        [
            Fila((C.Factura, s1), (C.FechaDeVenta, Fecha(corte)), (C.Deudor, clientes[0].Documento), (C.ValorOriginal, "100000"), (C.SaldoAlCorte, "100000"),
                (C.Resultado, "Migrated"), (C.NumeroDeCredito, "CR-0001")),
            Fila((C.Factura, s2), (C.FechaDeVenta, Fecha(corte)), (C.Deudor, clientes[1].Documento), (C.ValorOriginal, "200000"), (C.CobrosExternos, "200000"),
                (C.SaldoAlCorte, "0"), (C.Resultado, "CancelledBeforeCutoff"), (C.Linea, Linea)),
        ]);

        // Un plan que no suma el capital: aplicar no guarda nada y lo dice con parte, fila y columna.
        var descuadrada = await AplicarAsync(http, jefe, carga);
        descuadrada.IsSuccessStatusCode.Should().BeFalse(await descuadrada.Content.ReadAsStringAsync());
        var entrePartes = await CarteraE2E.LeerAsync(descuadrada);
        entrePartes.GetProperty("code").GetString().Should().Be("Lending.Migration.CrossPartErrors");
        var ultimo = numeros[^1];
        Errores(entrePartes).Should().Contain(e => e.GetProperty("message").GetString()!.Contains(ultimo, StringComparison.Ordinal)
            && e.GetProperty("row").GetInt32() > 0 && !string.IsNullOrEmpty(e.GetProperty("column").GetString()), $"el error nombra al crédito: {entrePartes}");
        (await CarteraE2E.GetAsync(http, jefe, $"{Cargas}/{carga}")).GetProperty("status").GetInt32().Should().Be(1, "sigue en borrador");

        // replace=true deja las dos tandas reemplazadas; se vuelven a subir en dos archivos, ya cuadradas.
        await CargarAsync(http, jefe, carga, P.Installments, CuotasDe(numeros.Take(50), false), reemplazar: true, archivo: "cuotas-1b.csv");
        await CargarAsync(http, jefe, carga, P.Installments, CuotasDe(numeros.Skip(50), false), archivo: "cuotas-2b.csv");
        var tandas = (await CarteraE2E.GetAsync(http, jefe, $"{Cargas}/{carga}")).GetProperty("parts").EnumerateArray()
            .Where(t => t.GetProperty("part").GetString() == P.Installments).ToList();
        tandas.Should().HaveCount(4);
        tandas.Count(t => t.GetProperty("status").GetInt32() == 2).Should().Be(2, "las dos primeras quedaron reemplazadas");

        // --------------------------------------------------------------------- aplicar sólo guarda (US8-9) --
        var aplicar = await AplicarAsync(http, jefe, carga);
        aplicar.StatusCode.Should().Be(HttpStatusCode.OK, await aplicar.Content.ReadAsStringAsync());
        var aplicada = await CarteraE2E.LeerAsync(aplicar);
        aplicada.GetProperty("status").GetInt32().Should().Be(2);
        aplicada.GetProperty("replacedLoadPublicId").GetGuid().Should().Be(ensayo, "la aplicada anterior queda reemplazada");
        aplicada.GetProperty("balanceCheck").GetProperty("isBalanced").GetBoolean().Should().BeFalse();
        (await CarteraE2E.GetAsync(http, jefe, $"{Cargas}/{ensayo}")).GetProperty("status").GetInt32().Should().Be(3);
        (await CarteraE2E.GetAsync(http, admin, "/api/lending/loans?pageSize=5")).GetProperty("totalCount").GetInt64().Should().Be(0, "aplicar no hace nacer créditos");

        // Cuadre contra Contabilidad (US8-2): lo de Cartera frente a lo contable, sin documentos ni nombres.
        var cuadre = await CarteraE2E.GetAsync(http, admin, $"{Cargas}/{carga}/balance-check");
        var filas = cuadre.GetProperty("rows").EnumerateArray().ToList();
        filas.Should().NotBeEmpty();
        filas.Sum(f => f.GetProperty("lending").GetDecimal()).Should().BeGreaterThanOrEqualTo(99_100_000m, "el capital de los cien créditos está en el cuadre");
        filas.Should().Contain(f => f.GetProperty("difference").GetDecimal() != 0m, "sin la matriz contable, el capital no cuadra");
        cuadre.ToString().Should().NotContain(clientes[0].Documento).And.NotContain("Deudor01", "el cuadre no trae documentos ni nombres");

        // Conciliación de ventas: migrada, cancelada antes del corte, no migradas con saldo; la de servicios se marca fuera de Cartera.
        var conciliacion = (await CarteraE2E.GetAsync(http, jefe, $"{Cargas}/{carga}/reconciliation?pageSize=50")).GetProperty("items").EnumerateArray()
            .ToDictionary(v => v.GetProperty("invoiceNumber").GetString()!);
        conciliacion[s1].GetProperty("classification").GetInt32().Should().Be(1);
        conciliacion[s2].GetProperty("classification").GetInt32().Should().Be(2);
        conciliacion[s3].GetProperty("classification").GetInt32().Should().Be(3);
        conciliacion[s4].GetProperty("classification").GetInt32().Should().Be(3);
        var marca = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Cargas}/{carga}/reconciliation/marks", new
        {
            messagePublicId = conciliacion[s4].GetProperty("messagePublicId").GetGuid(), classification = "OutsideLendingD29",
            reason = "Servicio a asociado confirmado antes del crédito sin Cartera (e2e)",
        });
        marca.StatusCode.Should().Be(HttpStatusCode.OK, await marca.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(marca)).GetProperty("classification").GetInt32().Should().Be(4);

        // ------------------------------------------------------------------------------------------- activar --
        // Decisión del dueño del 2026-10-18: los requisitos de la activación se exigen en todos los ambientes.
        await CarteraE2E.RequisitosDeActivacionAsync(fx, coop, corte);
        var punto = await CarteraE2E.ActaDeActivacionAsync(http, coop, carga, corte);
        var conductor = new ConductorDelDespachador(fx);
        using (conductor.Adelantar(TimeSpan.FromDays(1)))
        {
            object Orden(DateOnly fecha, Guid? cobros = null) => new
            {
                migrationLoadPublicId = carga, activationDate = fecha, provisionalCollectionsAttachmentPublicId = cobros, sessionItemPublicId = punto,
                acknowledgeWarnings = true, reason = "Arranque de Cartera con la cartera vigente (e2e)", payrollWarningAcknowledged = true,
            };

            var fueraDeFecha = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, Orden(corte.AddDays(2)));
            (await CarteraE2E.CodigoDeErrorAsync(fueraDeFecha)).Should().Be("Lending.Activation.CutoffInvalid");
            var sinAceptar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, Orden(corte.AddDays(1)));
            (await CarteraE2E.CodigoDeErrorAsync(sinAceptar)).Should().Be("Lending.Activation.DifferencesNotAccepted", "las diferencias no están aceptadas (US8-3)");

            // La secretaría del consejo acepta (no quien cargó) con el punto del acta: las del cuadre y las dos ventas.
            var cuadreActual = await CarteraE2E.GetAsync(http, admin, $"{Cargas}/{carga}/balance-check");
            var diferencias = cuadreActual.GetProperty("differences").EnumerateArray().Where(d => d.GetProperty("acceptedAt").ValueKind == JsonValueKind.Null)
                .Select(d => d.GetProperty("publicId").GetGuid())
                .Concat((await CarteraE2E.GetAsync(http, jefe, $"{Cargas}/{carga}/reconciliation?pageSize=50")).GetProperty("items").EnumerateArray()
                    .Where(v => v.GetProperty("differencePublicId").ValueKind != JsonValueKind.Null && !v.GetProperty("accepted").GetBoolean())
                    .Select(v => v.GetProperty("differencePublicId").GetGuid()))
                .ToList();
            diferencias.Should().HaveCountGreaterThan(2, "el cuadre más la venta no migrada y la de servicios");
            var secretaria = await UsuariosDeCartera.IngresarAsync(http, usuarios.Secretaria);
            var aceptar = await CarteraE2E.ConClaveAsync(http, secretaria, HttpMethod.Post, $"{Cargas}/{carga}/differences/accept", new
            {
                differencePublicIds = diferencias, sessionItemPublicId = punto, reason = "Diferencias del sistema de origen aprobadas por el consejo (e2e)",
            });
            aceptar.StatusCode.Should().Be(HttpStatusCode.OK, await aceptar.Content.ReadAsStringAsync());

            // La revisión de cobros provisionales no admite una venta hasta el corte (sus cobros los trajo la parte sales).
            var cobros = Encoding.UTF8.GetBytes(
                $"{C.Factura};{C.Valor};{C.FechaValor};{C.ReferenciaContable};{C.Observaciones}\n{s3};50000;{Fecha(corte.AddDays(1))};RC-000001;\n");
            var revisionDeCobros = await RevisarCobrosAsync(http, admin, cobros);
            revisionDeCobros.IsSuccessStatusCode.Should().BeFalse(await revisionDeCobros.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(revisionDeCobros)).Should().Be("Import.Invalid");

            // Activar (US8-4); la misma clave devuelve la misma activación.
            var clave = Guid.NewGuid();
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var activar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, Orden(corte.AddDays(1)), clave);
            reloj.Stop();
            activar.StatusCode.Should().Be(HttpStatusCode.Created, await activar.Content.ReadAsStringAsync());
            salida.WriteLine($"Activación de {Creditos} créditos migrados: {reloj.Elapsed.TotalMilliseconds:N0} ms.");
            var activacion = await CarteraE2E.LeerAsync(activar);
            activacion.GetProperty("loans").GetInt32().Should().Be(Creditos);
            activacion.GetProperty("openingTransactions").GetInt32().Should().BeGreaterThanOrEqualTo(Creditos);
            activacion.GetProperty("baseClosePublicId").ValueKind.Should().NotBe(JsonValueKind.Null, "nace el cierre base");
            activacion.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("type").GetString()).Should().Contain("CarteraActivada");
            var repetida = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, Orden(corte.AddDays(1)), clave);
            repetida.IsSuccessStatusCode.Should().BeTrue(await repetida.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(repetida)).GetProperty("activationPublicId").GetGuid()
                .Should().Be(activacion.GetProperty("activationPublicId").GetGuid(), "la misma clave, una sola activación");

            var estado = await CarteraE2E.GetAsync(http, admin, Activacion);
            estado.GetProperty("activationDate").GetString().Should().Be(Fecha(corte.AddDays(1)));
            (await CarteraE2E.GetAsync(http, jefe, $"{Cargas}/{carga}")).GetProperty("status").GetInt32().Should().Be(4);
            (await CarteraE2E.GetAsync(http, admin, "/api/lending/loans?pageSize=5")).GetProperty("totalCount").GetInt64()
                .Should().Be(Creditos + 1, "los cien migrados y la obligación en cero de la venta cancelada antes del corte");

            // Los saldos iniciales no son hechos contables: ningún mensaje de Cartera a Contabilidad salvo los informativos.
            var hechos = await ContabilidadDeInventarioE2E.EnteroAsync(fx, ventas.Inv,
                """SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" IN ('CreditoDesembolsado','GarantiaConstituida','CreditoCastigado','InteresCausado','MoraCausada','DeterioroCalculado','CupoRotativoOtorgado')""",
                "SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] IN ('CreditoDesembolsado','GarantiaConstituida','CreditoCastigado','InteresCausado','MoraCausada','DeterioroCalculado','CupoRotativoOtorgado')");
            hechos.Should().Be(0, "el saldo inicial migrado no emite hechos contables (FR-140)");
            var destino = await ContabilidadDeInventarioE2E.EnteroAsync(fx, ventas.Inv,
                """SELECT COUNT(*) FROM dbo."COR_IntegrationParticipants" WHERE "Code" = 'CAR' AND "IsEnabled" = TRUE AND "ValidTo" IS NULL""",
                "SELECT COUNT(*) FROM [dbo].[COR_IntegrationParticipants] WHERE [Code] = 'CAR' AND [IsEnabled] = 1 AND [ValidTo] IS NULL");
            destino.Should().Be(1, "la activación habilita el destino Lending");
            (await CarteraE2E.GetAsync(http, jefe, $"{CierreE2E.Cierres}?kind=Base")).EnumerateArray().Should().ContainSingle("nace el cierre base");

            // La liquidación a C de un crédito migrado es lo cargado.
            var cr2 = (await CarteraE2E.GetAsync(http, admin, "/api/lending/loans?search=CR-0002")).GetProperty("items").EnumerateArray()
                .Single(l => l.GetProperty("number").GetString() == "CR-0002").GetProperty("publicId").GetGuid();
            var liquidacion = await CarteraE2E.GetAsync(http, admin, $"/api/lending/loans/{cr2}/settlement?date={Fecha(corte)}");
            liquidacion.GetProperty("byConcept").GetProperty("Principal").GetDecimal().Should().Be(1_000_000m);

            // Las ventas acumuladas llegan a Cartera: la migrada se enlaza a su crédito; la no migrada aceptada se rechaza sin reproceso.
            await CarteraE2E.DespacharAsync(fx, tenant, 4);
            var migrada = (await VentasConCarteraE2E.PagoACreditoAsync(http, ventas.Cajero1.Token, ventasPorMonto[100_000m])).GetProperty("lendingObligation");
            migrada.ValueKind.Should().Be(JsonValueKind.Object, "Comercial recibió la respuesta de la venta migrada");
            migrada.GetProperty("outcome").GetString().Should().Be("Created", migrada.ToString());
            migrada.GetProperty("obligationNumber").GetString().Should().Be("CR-0001", "la venta migrada queda con el número migrado");
            var noMigrada = (await VentasConCarteraE2E.PagoACreditoAsync(http, ventas.Cajero1.Token, ventasPorMonto[300_000m])).GetProperty("lendingObligation");
            noMigrada.ValueKind.Should().Be(JsonValueKind.Object, "Comercial recibió la respuesta de la venta no migrada");
            noMigrada.GetProperty("outcome").GetString().Should().Be("Rejected", noMigrada.ToString());
            noMigrada.GetProperty("retryable").GetBoolean().Should().BeFalse();

            // US13 (T0769, US13-4): el paz y salvo general del cliente de la venta marcada fuera de Cartera no se niega por ella: se emite y la
            // menciona como venta que no hace parte de Cartera (la nota queda en el documento y en su PDF).
            await DocumentosE2E.FirmanteAsync(fx, tenant, admin, corte.AddDays(1));
            var general = await DocumentosE2E.EmitidoAsync(http, admin, DocumentosE2E.Pedido("GeneralClearance", clientes[3].Persona, null));
            var detalleDelGeneral = await CarteraE2E.GetAsync(http, admin, $"{DocumentosE2E.Documentos}/{general.GetProperty("documentPublicId").GetGuid()}");
            detalleDelGeneral.GetProperty("outsideLendingNote").GetString().Should().Contain(s4, "la venta aceptada sin migrar queda mencionada (FR-143)");
            detalleDelGeneral.GetProperty("document").GetProperty("kind").GetString().Should().Be("GeneralClearance");
        }
    }

    private static Task<HttpResponseMessage> RevisarCobrosAsync(HttpClient http, string token, byte[] contenido)
    {
        var formulario = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(contenido);
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        formulario.Add(bytes, "file", "cobros.csv");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"{Activacion}/provisional-collections/review") { Content = formulario };
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }
}
