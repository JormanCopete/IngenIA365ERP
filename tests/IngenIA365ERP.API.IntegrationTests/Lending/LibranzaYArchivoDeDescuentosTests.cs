using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.LibranzaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0819 (feature 013, K2, US10; quickstart §1.7 y §4; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): la libranza por HTTP con
/// tokens reales, en la cooperativa aislada «libranza» (<see cref="LibranzaE2E.EscenarioAsync"/>), con el procesador apagado y el ciclo diario
/// ordenado y corrido a mano con el reloj de la suite. Las fechas salen de la fecha <b>exigible</b> de la primera cuota (art. 829), no del día en que
/// corre la prueba:
/// <list type="number">
/// <item>radicar por libranza una cuota que no cabe en la capacidad de descuento responde 422 <c>Lending.Payroll.DeductionCapacityExceeded</c> con lo
/// disponible (FR-110);</item>
/// <item>con 20 créditos de la pagaduría, la vista previa y el archivo del período (20 altas, huella SHA-256, enlace firmado); regenerarlo antes del
/// envío deja la versión anterior reemplazada; el recaudador de prueba confirma el envío (queda <c>Sent</c> con el radicado) y volver a generar el
/// período responde <c>Lending.PayrollFile.AlreadySent</c> (I9);</item>
/// <item>la respuesta de la pagaduría con 17 descontados y 3 no aplicados (dos licencias y un retiro): los 3 quedan en «No aplicados» con su motivo y
/// el seguimiento automático del convenio; los 17 quedan pendientes de giro y, a lo largo de dos ciclos diarios dentro del umbral, sin días de mora
/// para el deudor, mientras los no aplicados sí cuentan mora desde su exigible (FR-114, T30);</item>
/// <item>el giro de una línea por el contrato con su referencia (fecha valor = la del descuento, A9) levanta su pendiente;</item>
/// <item>trasladar una libranza a la otra pagaduría conserva la fecha de libranza original (FR-116); resolver un no aplicado dos veces responde
/// <c>Lending.InstallmentNonDeduction.AlreadyResolved</c>;</item>
/// <item>el cuadre <c>payroll-deduction-reconciliation</c> del período: 20 líneas, lo enviado, lo descontado, lo pendiente de giro, lo aplicado y lo no
/// aplicado con su motivo; y <c>payroll-agreements</c> muestra lo pendiente de giro de la pagaduría.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class LibranzaYArchivoDeDescuentosTests(CentralIdentityApiFixture fx)
{
    private const int Creditos = 20;
    private const int NoDescontados = 3;

    [Fact]
    public async Task Capacidad_archivo_respuesta_pendiente_de_giro_giro_traslado_y_cuadre()
    {
        var esc = await EscenarioAsync(fx);
        var tenant = esc.Coop.TenantPublicId;
        var h = esc.Hoy;
        using var http = fx.CreateClient();

        // (1) La capacidad de descuento al radicar: 5.000.000 a 12 meses no cabe en el 40 % de 200.000 certificados.
        var accesos = await AccesosAsync(http, esc);
        var (excede, _) = await DesembolsoE2E.DeudorAsync(http, accesos.Admin, "Excede");
        var asesor = accesos.Asesor;
        var borrador = await CarteraE2E.ExitoAsync(http, asesor, HttpMethod.Post, OriginacionE2E.Solicitudes,
            Solicitud(esc, excede, 5_000_000m, 200_000m, "EMP-EXCEDE"));
        var radicar = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, $"{OriginacionE2E.Solicitudes}/{borrador.GetProperty("publicId").GetGuid()}/submit",
            new { rowVersion = borrador.GetProperty("rowVersion").GetString() });
        radicar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await radicar.Content.ReadAsStringAsync());
        var sobre = await CarteraE2E.LeerAsync(radicar);
        sobre.GetProperty("code").GetString().Should().Be("Lending.Payroll.DeductionCapacityExceeded");
        var datos = sobre.GetProperty("data");
        datos.GetProperty("available").GetDecimal().Should().BeLessThan(datos.GetProperty("requested").GetDecimal());
        datos.GetProperty("available").GetDecimal().Should().BeLessThanOrEqualTo(200_000m * Tope, "lo disponible no pasa del 40 % de lo certificado");

        // (2) Veinte créditos de la pagaduría, desembolsados hoy.
        var creditos = new List<(Guid Credito, string Numero, Guid Persona, string Empleado)>();
        for (var i = 1; i <= Creditos; i++) creditos.Add(await CreditoPorLibranzaAsync(http, esc, accesos, $"Libranza{i:00}"));
        var jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);
        var (_, vence, _) = await PagosE2E.CuotaAsync(http, jefe, creditos[0].Credito);
        var exigible = await PagosE2E.ExigibleAsync(http, jefe, creditos[0].Credito);
        foreach (var c in creditos.Skip(1))
            (await PagosE2E.ExigibleAsync(http, jefe, c.Credito)).Should().Be(exigible, "todos se desembolsaron el mismo día con el mismo plan");
        var (anio, mes) = (vence.Year, vence.Month);

        // La vista previa no escribe; el archivo del período: 20 altas, huella y enlace firmado.
        var adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var previa = await CarteraE2E.ExitoAsync(http, adm, HttpMethod.Post, $"{Archivos}/preview", new { agreementPublicId = esc.Convenio, year = anio, month = mes });
        previa.GetProperty("lines").GetArrayLength().Should().Be(Creditos);
        previa.GetProperty("newCount").GetInt32().Should().Be(Creditos, "sin archivo enviado antes, todas son altas");
        (await CarteraE2E.GetAsync(http, adm, $"{Archivos}?agreementPublicId={esc.Convenio}")).GetProperty("totalCount").GetInt64()
            .Should().Be(0, "la vista previa no guarda nada");

        var primero = await GenerarAsync(http, adm, esc.Convenio, anio, mes);
        primero.GetProperty("lines").GetInt32().Should().Be(Creditos);
        primero.GetProperty("status").GetString().Should().Be("Generated");
        primero.GetProperty("sha256").GetString().Should().MatchRegex("^[0-9a-fA-F]{64}$");
        var enlace = await CarteraE2E.ExitoAsync(http, adm, HttpMethod.Post, $"{Archivos}/{primero.GetProperty("publicId").GetGuid()}/download-link");
        enlace.GetProperty("url").GetString().Should().NotBeNullOrWhiteSpace();

        // Regenerar antes del envío: versión 2 y la 1 reemplazada.
        var archivo = await GenerarAsync(http, adm, esc.Convenio, anio, mes);
        var archivoId = archivo.GetProperty("publicId").GetGuid();
        archivo.GetProperty("version").GetInt32().Should().Be(2);
        (await ArchivoAsync(http, adm, primero.GetProperty("publicId").GetGuid())).Archivo.GetProperty("status").GetString().Should().Be("Superseded");

        // El recaudador de prueba confirma el envío: queda Sent con el radicado; el período ya no se regenera (I9).
        await ConfirmarEnvioAsync(fx, http, esc, archivoId, "RAD-E2E-0001");
        var (enviado, lineas) = await ArchivoAsync(http, adm, archivoId);
        enviado.GetProperty("status").GetString().Should().Be("Sent");
        enviado.GetProperty("paymasterReference").GetString().Should().Be("RAD-E2E-0001");
        lineas.Should().HaveCount(Creditos);
        lineas.Select(l => l.GetProperty("loanPublicId").GetGuid()).Should().BeEquivalentTo(creditos.Select(c => c.Credito));
        lineas.Should().OnlyContain(l => DateOnly.Parse(l.GetProperty("libranzaDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture) == h);
        var otraVez = await CarteraE2E.ConClaveAsync(http, adm, HttpMethod.Post, Archivos, new { agreementPublicId = esc.Convenio, year = anio, month = mes });
        (await CarteraE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Lending.PayrollFile.AlreadySent");

        var lineaDe = lineas.ToDictionary(l => l.GetProperty("loanPublicId").GetGuid());
        var noDescontados = creditos.Take(NoDescontados).ToList();
        var descontados = creditos.Skip(NoDescontados).ToList();
        decimal Total(Guid credito) => lineaDe[credito].GetProperty("total").GetDecimal();

        // (3) El día exigible la pagaduría responde: 17 descontados ese día, dos licencias y un retiro.
        using (RelojEn(fx, exigible))
        {
            await ResponderAsync(fx, http, esc, archivoId,
                descontados.Select(c => (c.Numero, "00", Total(c.Credito), (DateOnly?)exigible))
                    .Concat([(noDescontados[0].Numero, "07", 0m, null), (noDescontados[1].Numero, "07", 0m, null), (noDescontados[2].Numero, "08", 0m, null)]));

            adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
            var abiertos = (await CarteraE2E.GetAsync(http, adm, $"{NoAplicados}?agreementPublicId={esc.Convenio}&origin=PaymasterResult&status=Open"))
                .GetProperty("items").EnumerateArray().ToList();
            abiertos.Select(n => n.GetProperty("loanPublicId").GetGuid()).Should().BeEquivalentTo(noDescontados.Select(c => c.Credito));
            abiertos.Select(n => n.GetProperty("reason").GetString()).Should().BeEquivalentTo(["Leave", "Leave", "Termination"], "el código se traduce al motivo");
            abiertos.Should().OnlyContain(n => n.GetProperty("lastAction").ValueKind == JsonValueKind.String, "el convenio deja su seguimiento automático");

            var pendientes = (await CarteraE2E.GetAsync(http, adm, $"{Convenios}/{esc.Convenio}/pending-transfers")).EnumerateArray().ToList();
            var pendiente = pendientes.Should().ContainSingle().Subject;
            pendiente.GetProperty("lines").GetInt32().Should().Be(Creditos - NoDescontados);
            pendiente.GetProperty("amount").GetDecimal().Should().Be(descontados.Sum(c => Total(c.Credito)));
            pendiente.GetProperty("thresholdExceeded").GetBoolean().Should().BeFalse();
        }

        // Dos ciclos diarios dentro del umbral: lo descontado no gira todavía y el deudor no está en mora; lo no aplicado sí.
        foreach (var dias in new[] { 3, 10 })
        {
            using (RelojEn(fx, exigible.AddDays(dias)))
            {
                jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);
                await CicloDeHoyAsync(fx, http, jefe, tenant);
                foreach (var c in descontados)
                    (await DiasDeMoraAsync(http, jefe, c.Numero)).Should().Be(0, $"{c.Numero}: descontado y pendiente de giro dentro del umbral, sin mora para el deudor");
                foreach (var c in noDescontados)
                    (await DiasDeMoraAsync(http, jefe, c.Numero)).Should().Be(dias, $"{c.Numero}: no aplicado, la mora cuenta desde su exigible");
            }
        }

        using (RelojEn(fx, exigible.AddDays(10)))
        {
            adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
            var pendiente = (await CarteraE2E.GetAsync(http, adm, $"{Convenios}/{esc.Convenio}/pending-transfers")).EnumerateArray().Single();
            pendiente.GetProperty("paymasterArrearsDays").GetInt32().Should().Be(10 - VentanaDeGiro, "la pagaduría está en mora desde que venció su ventana de giro");
            pendiente.GetProperty("thresholdExceeded").GetBoolean().Should().BeFalse($"{10 - VentanaDeGiro} días no superan el umbral de {Umbral}");

            // (4) El giro de una línea por el contrato, con su referencia: fecha valor = la del descuento; el pendiente se levanta.
            var girado = descontados[0];
            var pago = PagosE2E.Pago(girado.Credito, esc.Pagaduria, "FreeAmount", Total(girado.Credito), exigible.AddDays(10), medio: "ExternalPaymasterDeduction");
            pago["deductionFileLinePublicId"] = lineaDe[girado.Credito].GetProperty("publicId").GetGuid();
            var pruebas = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Pruebas);
            var lote = await CarteraE2E.ConClaveAsync(http, pruebas, HttpMethod.Post, $"{PagosE2E.Recaudador}/payments", new { items = new[] { pago }, interactive = false });
            lote.StatusCode.Should().Be(HttpStatusCode.OK, await lote.Content.ReadAsStringAsync());
            var respuesta = (await CarteraE2E.LeerAsync(lote)).GetProperty("payments")[0];
            PagosE2E.Resultado(respuesta).Should().Be(PagosE2E.Aplicado, respuesta.ToString());
            var comprobante = respuesta.GetProperty("value");
            DateOnly.Parse(comprobante.GetProperty("valueDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                .Should().Be(exigible, "la fecha valor del giro es la del descuento (A9)");
            pendiente = (await CarteraE2E.GetAsync(http, adm, $"{Convenios}/{esc.Convenio}/pending-transfers")).EnumerateArray().Single();
            pendiente.GetProperty("lines").GetInt32().Should().Be(Creditos - NoDescontados - 1, "el giro levanta el pendiente de su línea");

            // (5) El traslado a la otra pagaduría conserva la fecha de libranza.
            var trasladado = descontados[1];
            var traslado = await CarteraE2E.ConClaveAsync(http, adm, HttpMethod.Post, $"/api/lending/loans/{trasladado.Credito}/payroll-transfer", new
            {
                newAgreementPublicId = esc.OtroConvenio, employeeId = "GOB-00077", certificationAttachmentPublicId = esc.Certificacion,
                certificationDate = exigible.AddDays(9), certifiedNetIncome = IngresoCertificado, reason = "Traslado a la Gobernación (e2e)",
            });
            traslado.StatusCode.Should().Be(HttpStatusCode.OK, await traslado.Content.ReadAsStringAsync());
            var hecho = await CarteraE2E.LeerAsync(traslado);
            hecho.GetProperty("agreementCode").GetString().Should().Be("PAGGOB");
            hecho.GetProperty("libranzaDate").GetString().Should().Be(h.ToString("yyyy-MM-dd"), "el traslado conserva la fecha de libranza (prelación, FR-111)");

            // Resolver un no aplicado: una vez, con aviso; la segunda, ya resuelto.
            var abierto = (await CarteraE2E.GetAsync(http, adm, $"{NoAplicados}?agreementPublicId={esc.Convenio}&status=Open")).GetProperty("items")
                .EnumerateArray().First(n => n.GetProperty("loanPublicId").GetGuid() == noDescontados[2].Credito);
            var resolver = $"{NoAplicados}/{abierto.GetProperty("publicId").GetGuid()}/resolve";
            var resuelto = await CarteraE2E.ConClaveAsync(http, adm, HttpMethod.Post, resolver, new { action = "SentToCollection", reason = "Retirado: pasa a cobranza (e2e)" });
            resuelto.StatusCode.Should().Be(HttpStatusCode.OK, await resuelto.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(resuelto)).GetProperty("resolved").GetBoolean().Should().BeTrue();
            var otra = await CarteraE2E.ConClaveAsync(http, adm, HttpMethod.Post, resolver, new { action = "Closed", reason = "Otra vez (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(otra)).Should().Be("Lending.InstallmentNonDeduction.AlreadyResolved");

            // (6) El cuadre del período y la vista de pagadurías.
            var cuadre = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, adm,
                $"{Informes}/payroll-deduction-reconciliation?agreement={esc.Convenio}&year={anio}&month={mes}"));
            cuadre.Filas.Should().HaveCount(Creditos);
            decimal Suma(string columna) => cuadre.Filas.Sum(f => ContabilidadE2E.Tabla.Numero(f, cuadre.Indice(columna)));
            Suma("Enviado").Should().Be(creditos.Sum(c => Total(c.Credito)));
            Suma("Descontado").Should().Be(descontados.Sum(c => Total(c.Credito)));
            Suma("Pendiente de giro").Should().Be(descontados.Skip(1).Sum(c => Total(c.Credito)));
            Suma("Aplicado").Should().Be(Total(girado.Credito));
            Suma("No aplicado").Should().Be(noDescontados.Sum(c => Total(c.Credito)));
            cuadre.Filas.Count(f => ContabilidadE2E.Tabla.Celda(f, cuadre.Indice("Motivo")) is { ValueKind: JsonValueKind.String } m
                                    && !string.IsNullOrWhiteSpace(m.GetString())).Should().Be(NoDescontados, "cada no aplicado con su motivo (SC-016)");

            var pagadurias = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, adm, $"{Informes}/payroll-agreements"));
            var fila = pagadurias.Filas.Single(f => ContabilidadE2E.Tabla.Celda(f, pagadurias.Indice("Pagaduría")).GetString()!.Contains("PAGSED"));
            ContabilidadE2E.Tabla.Numero(fila, pagadurias.Indice("Descontado pendiente de giro")).Should().Be(descontados.Skip(1).Sum(c => Total(c.Credito)));
        }
    }

    /// <summary>Los días de mora de la posición diaria vigente del crédito (la lista de créditos).</summary>
    private static async Task<int> DiasDeMoraAsync(HttpClient http, string token, string numero)
    {
        var lista = await CarteraE2E.GetAsync(http, token, $"{DesembolsoE2E.Creditos}?search={Uri.EscapeDataString(numero)}");
        return lista.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("number").GetString() == numero).GetProperty("daysPastDue").GetInt32();
    }

}
