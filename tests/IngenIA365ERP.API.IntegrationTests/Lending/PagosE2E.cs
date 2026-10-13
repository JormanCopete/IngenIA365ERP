using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Los ayudantes de los pagos (feature 013, US5, T0527–T0531): el recaudador de prueba (<c>TST</c>, clase <c>Test</c>) ejercita el contrato de
/// obligaciones por sus rutas de <c>/api/lending/test-doubles/collector</c> con el usuario <c>pruebas</c>, como lo haría Recaudo (la transacción la
/// abre el comando del doble alrededor del contrato); las rutas de pantalla simulan y reversan con <c>jefe</c>. Las respuestas del contrato son
/// <c>RespuestaDelContratoDto</c>: <c>result</c> 1 <c>Applied</c>, 2 <c>AlreadyApplied</c>, 3 <c>Rejected</c>, 4 <c>Answered</c> (la API devuelve
/// los enums como número). (nuevo)
/// </summary>
public static class PagosE2E
{
    public const string Recaudador = "/api/lending/test-doubles/collector";
    public const int Aplicado = 1;
    public const int YaAplicado = 2;
    public const int Rechazado = 3;
    public const int Respondido = 4;

    /// <summary>Las modalidades de <c>ModalidadDePago</c> por nombre (la API las acepta por nombre y las devuelve como número).</summary>
    public static readonly string[] Modalidades =
        ["FullPayoff", "PastDue", "CycleInstallment", "Advance", "ExtraordinaryPrincipal", "SpecificInstallment", "FreeAmount"];

    /// <summary>Una clave de negocio nueva del recaudador de prueba.</summary>
    public static string Clave() => $"TST:{Guid.NewGuid():N}";

    /// <summary>Un ítem del lote (<c>PagoAAplicar</c>) por caja, con el deudor como pagador y un recibo de prueba.</summary>
    public static Dictionary<string, object?> Pago(Guid credito, Guid pagador, string modalidad, decimal valor, DateOnly fechaValor, string? clave = null,
        string? eleccion = null, int? cuota = null, string? simulacion = null, string medio = "Cash") => new()
    {
        ["idempotencyKey"] = clave ?? Clave(),
        ["obligationPublicId"] = credito,
        ["modality"] = modalidad,
        ["amount"] = valor,
        ["valueDate"] = fechaValor,
        ["meansClass"] = medio,
        ["payerPublicId"] = pagador,
        ["receipt"] = new { number = $"RC-{Guid.NewGuid():N}"[..14], date = fechaValor, officePublicId = (Guid?)null, meansReference = (string?)null, documentKind = "Recibo" },
        ["choice"] = eleccion,
        ["installmentNumber"] = cuota,
        ["simulationId"] = simulacion,
    };

    /// <summary>Aplica un lote con <c>pruebas</c> y exige el 200; devuelve la respuesta del lote (<c>payments[]</c>, <c>notDeducted[]</c>).</summary>
    public static async Task<JsonElement> AplicarAsync(HttpClient http, UsuariosDeCartera usuarios, bool interactiva, params Dictionary<string, object?>[] items)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, usuarios.Pruebas.Token, HttpMethod.Post, $"{Recaudador}/payments", new { items, interactive = interactiva });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aplicar: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>Aplica un solo pago y devuelve su respuesta (<c>RespuestaDelContratoDto</c>).</summary>
    public static async Task<JsonElement> AplicarUnoAsync(HttpClient http, UsuariosDeCartera usuarios, Dictionary<string, object?> pago, bool interactiva = false) =>
        (await AplicarAsync(http, usuarios, interactiva, pago)).GetProperty("payments")[0];

    /// <summary>Aplica un pago y exige <c>Applied</c>; devuelve el comprobante (<c>value</c>).</summary>
    public static async Task<JsonElement> AplicadoAsync(HttpClient http, UsuariosDeCartera usuarios, Dictionary<string, object?> pago, bool interactiva = false)
    {
        var r = await AplicarUnoAsync(http, usuarios, pago, interactiva);
        Resultado(r).Should().Be(Aplicado, r.ToString());
        return r.GetProperty("value");
    }

    /// <summary>Lo que lista el contrato de una persona (exige el 200 y <c>Answered</c>); devuelve el listado (<c>value</c>).</summary>
    public static async Task<JsonElement> ListarAsync(HttpClient http, UsuariosDeCartera usuarios, Guid persona)
    {
        var r = await CarteraE2E.GetAsync(http, usuarios.Pruebas.Token, $"{Recaudador}/obligations?personPublicId={persona}");
        Resultado(r).Should().Be(Respondido, r.ToString());
        r.GetProperty("code").GetString().Should().Be("Lending.ObligationContract.Listed");
        return r.GetProperty("value");
    }

    /// <summary>La obligación del listado.</summary>
    public static async Task<JsonElement> ObligacionAsync(HttpClient http, UsuariosDeCartera usuarios, Guid persona, Guid credito) =>
        (await ListarAsync(http, usuarios, persona)).GetProperty("obligations").EnumerateArray()
            .Single(o => o.GetProperty("obligationPublicId").GetGuid() == credito);

    /// <summary>La simulación del contrato (exige el 200); devuelve la respuesta (<c>Answered</c> o <c>Rejected</c>).</summary>
    public static async Task<JsonElement> SimularAsync(HttpClient http, UsuariosDeCartera usuarios, Guid credito, string modalidad, decimal valor,
        DateOnly fechaValor, string? eleccion = null, int? cuota = null, string? claveAReversar = null)
    {
        var resp = await CarteraE2E.EnviarAsync(http, usuarios.Pruebas.Token, HttpMethod.Post, $"{Recaudador}/simulations", new
        {
            obligationPublicId = credito, modality = modalidad, amount = valor, valueDate = fechaValor, prepaymentChoice = eleccion, installmentNumber = cuota,
            paymentIdempotencyKeyToReverse = claveAReversar,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"simular: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>Reversa un pago del recaudador de prueba por su clave; devuelve la respuesta del contrato.</summary>
    public static async Task<JsonElement> ReversarPorClaveAsync(HttpClient http, UsuariosDeCartera usuarios, string clave, string motivo, Guid? claveDePantalla = null)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, usuarios.Pruebas.Token, HttpMethod.Post, $"{Recaudador}/payments/{Uri.EscapeDataString(clave)}/reverse",
            new { reason = motivo }, claveDePantalla);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"reversar {clave}: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>Resuelve un saldo por el recaudador de prueba; devuelve la respuesta del contrato.</summary>
    public static async Task<JsonElement> ResolverAsync(HttpClient http, UsuariosDeCartera usuarios, object cuerpo, Guid? claveDePantalla = null)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, usuarios.Pruebas.Token, HttpMethod.Post, $"{Recaudador}/credit-balances/resolve", cuerpo, claveDePantalla);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"resolver: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>El resultado de una respuesta del contrato.</summary>
    public static int Resultado(JsonElement respuesta) => respuesta.GetProperty("result").GetInt32();

    /// <summary>El código de una respuesta del contrato.</summary>
    public static string Codigo(JsonElement respuesta) => respuesta.GetProperty("code").GetString()!;

    /// <summary>Exige <c>Rejected</c> con <paramref name="codigo"/>.</summary>
    public static void Rechazada(JsonElement respuesta, string codigo)
    {
        Resultado(respuesta).Should().Be(Rechazado, respuesta.ToString());
        Codigo(respuesta).Should().Be(codigo, respuesta.ToString());
    }

    /// <summary>El valor de un campo de los avisos (<c>warnings</c>) del comprobante.</summary>
    public static IReadOnlyList<string> Avisos(JsonElement comprobante) =>
        comprobante.TryGetProperty("warnings", out var w) && w.ValueKind == JsonValueKind.Array ? w.EnumerateArray().Select(x => x.GetString()!).ToList() : [];

    // ------------------------------------------------------------------------------------------------ la base --

    /// <summary>Las operaciones de un crédito (clase, número, clave, reversada por).</summary>
    public static Task<List<(LoanOperationKind Clase, string Numero, string? Clave, long? ReversadaPor, Guid PublicId)>> OperacionesAsync(
        CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, Guid credito) =>
        DesembolsoE2E.EnLaBaseAsync(fx, esc, async db =>
            (await (from o in db.LoanOperations.AsNoTracking()
                    join l in db.Loans.AsNoTracking() on o.LoanId equals l.Id
                    where l.PublicId == credito
                    orderby o.Sequence
                    select new { o.Kind, o.Number, o.IdempotencyKey, o.ReversedByOperationId, o.PublicId }).ToListAsync())
            .Select(x => (x.Kind, x.Number, x.IdempotencyKey, x.ReversedByOperationId, x.PublicId)).ToList());

    /// <summary>Los tipos de mensaje que dejó en la bandeja algo con origen o relacionado <paramref name="origen"/>.</summary>
    public static Task<List<string>> MensajesAsync(CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, Guid origen) =>
        DesembolsoE2E.EnLaBaseAsync(fx, esc, db => db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == origen || m.RelatedPublicId == origen).Select(m => m.Type).ToListAsync());

    /// <summary>El estado del crédito en la base.</summary>
    public static Task<LoanStatus> EstadoAsync(CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, Guid credito) =>
        DesembolsoE2E.EnLaBaseAsync(fx, esc, db => db.Loans.AsNoTracking().Where(l => l.PublicId == credito).Select(l => l.Status).SingleAsync());

    /// <summary>La primera cuota del plan vigente (número, vencimiento, total).</summary>
    public static async Task<(int Numero, DateOnly Vence, decimal Total)> CuotaAsync(HttpClient http, string token, Guid credito, int numero = 1)
    {
        var plan = await CarteraE2E.GetAsync(http, token, $"{DesembolsoE2E.Creditos}/{credito}/schedule");
        var cuota = plan.GetProperty("installments").EnumerateArray().Single(c => c.GetProperty("number").GetInt32() == numero);
        return (numero, DateOnly.Parse(cuota.GetProperty("dueDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            cuota.GetProperty("total").GetDecimal());
    }
}
