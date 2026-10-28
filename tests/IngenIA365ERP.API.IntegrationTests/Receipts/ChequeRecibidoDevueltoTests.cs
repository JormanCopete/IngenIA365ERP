using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Lending.Deposits;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US15, T0215 («Independent Test»; quickstart recorrido H paso 1; FR-060, FR-083 a FR-086): con la clase cheque encendida (<c>Caja.AdmiteCheque</c>) y el
/// medio ofrecido en el punto antes de abrir la sesión, una persona paga en un recibo la cuota de su crédito y un aporte con un cheque recibido más efectivo —el efectivo
/// se imputa primero y el cheque al final, así que la cuota queda partida entre los dos medios—; el banco devuelve el cheque por fondos insuficientes: se reversan sólo
/// las porciones pagadas con el cheque (la del crédito y la del aporte), la del efectivo sigue aplicada en Cartera, el medio se da de baja, el cheque queda devuelto con su
/// causal, la oficina recibe la alerta con el texto para avisar al pagador, el girador aparece en la lista de devoluciones y el informe de cheques devueltos lo trae. Sobre
/// Cartera y Captaciones reales, en los dos motores. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ChequeRecibidoDevueltoTests(CentralIdentityApiFixture fx)
{
    private const string CodigoDelCheque = "CHCJ";

    [Fact]
    public async Task Devolver_el_cheque_reversa_solo_sus_porciones_y_el_efectivo_sigue_aplicado()
    {
        using var http = fx.CreateClient();
        Guid? medioCheque = null;
        var c = await CajaE2E.PreparadaAsync(fx, http, "cheque", async esc =>
        {
            await RecaudoYTesoreriaE2E.ParametroAsync(fx, esc.Coop, "RCD", "Caja.AdmiteCheque", "true", esc.Hoy,
                "La cooperativa recibe cheques en ventanilla (e2e del recorrido H)");
            medioCheque = await ConfiguracionDeRecaudoE2E.MedioAsync(http, esc.Coop.TokenAdmin, CodigoDelCheque, "Check", esc.Hoy.AddDays(-1));
            return [medioCheque.Value];
        });
        var t = c.Token;
        var hoy = c.Escenario.Hoy;
        var aporte = await CuentaDeAportesAsync(http, c);

        // La cuota del ciclo de A (74.397,28) con 50.000 en efectivo y 10.000 del cheque; el aporte de 40.000 todo con el cheque.
        var banco = await RecaudoYTesoreriaE2E.BancoAsync(http, t);
        var cheque = new { draweeBankPublicId = banco, checkNumber = "0004417", accountNumber = "9988776655", issueDate = hoy, samePlace = true };
        var op = await CajaE2E.OperacionAsync(http, c,
            [CajaE2E.Cuota(c.CreditoA, 60_000m), new { target = "Account", amount = 40_000m, productPublicId = aporte, accountModality = "Free" }],
            [CajaE2E.Medio(c.Efectivo, 50_000m), new { paymentMeansPublicId = medioCheque!.Value, amount = 50_000m, reference = "0004417", check = cheque }]);
        var recibo = await CajaE2E.ReciboAsync(http, c, op);
        recibo.GetProperty("applications").GetArrayLength().Should().Be(2, "una aplicación por ítem: la cuota y el aporte");

        var porcionesAntes = await PorcionesAsync(c, op);
        porcionesAntes.Should().HaveCount(3, "la cuota partida entre el efectivo y el cheque, y el aporte con el cheque");
        porcionesAntes.Where(p => p.Medio == CodigoDelCheque).Sum(p => p.Valor).Should().Be(50_000m);
        porcionesAntes.Should().OnlyContain(p => p.Estado == PortionState.Applied);
        var pagosAntes = await PagosVigentesAsync(c);
        pagosAntes.Should().HaveCount(2, "Cartera recibió las dos porciones de la cuota, cada una con su clave");

        var recibido = (await InventarioE2E.GetAsync(http, t, "/api/receipts/received-checks")).EnumerateArray()
            .Single(x => x.GetProperty("checkNumber").GetString() == "0004417");
        RecaudoYTesoreriaE2E.Enum<ReceivedCheckState>(recibido.GetProperty("status")).Should().Be(ReceivedCheckState.Received);
        recibido.GetProperty("receiptNumber").GetString().Should().Be(recibo.GetProperty("number").GetString());
        var chequeId = recibido.GetProperty("publicId").GetGuid();

        // Consignado y en canje (canje manual de R0); el banco lo devuelve por fondos insuficientes (causal 01 de la semilla).
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/receipts/received-checks/{chequeId}/deposit", new { note = "Consignado (e2e)" });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/receipts/received-checks/{chequeId}/clearing", new { note = "En canje (e2e)" });
        var causales = (await InventarioE2E.GetAsync(http, t, "/api/receipts/received-checks/return-causes")).EnumerateArray().ToList();
        causales.Should().Contain(x => x.GetProperty("code").GetString() == "01");
        var devuelto = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/receipts/received-checks/{chequeId}/return",
            new { causeCode = "01", note = "Fondos insuficientes (e2e)" });
        RecaudoYTesoreriaE2E.Enum<ReceivedCheckState>(devuelto.GetProperty("status")).Should().Be(ReceivedCheckState.Returned);
        devuelto.GetProperty("returnCauseCode").GetString().Should().Be("01");
        devuelto.GetProperty("payerReceivableNumber").ValueKind.Should().Be(JsonValueKind.Null, "todo lo del cheque estaba en porciones: no queda nada por cobrar al pagador");

        // Sólo las porciones del cheque se reversaron; la del efectivo sigue aplicada, también en Cartera.
        var porcionesDespues = await PorcionesAsync(c, op);
        porcionesDespues.Where(p => p.Medio == CodigoDelCheque).Should().OnlyContain(p => p.Estado == PortionState.Reversed);
        var delEfectivo = porcionesDespues.Where(p => p.Medio != CodigoDelCheque).ToList();
        delEfectivo.Should().ContainSingle();
        delEfectivo[0].Estado.Should().Be(PortionState.Applied);
        delEfectivo[0].Valor.Should().Be(50_000m);
        var pagosDespues = await PagosVigentesAsync(c);
        pagosDespues.Should().ContainSingle("la porción del cheque se reversó en Cartera; la del efectivo, no");
        var medios = (await InventarioE2E.GetAsync(http, t, $"/api/receipts/receipts/{op}")).GetProperty("means").EnumerateArray().ToList();
        RecaudoYTesoreriaE2E.Enum<ReceiptMeansState>(medios.Single(m => m.GetProperty("meansCode").GetString() == CodigoDelCheque).GetProperty("status"))
            .Should().Be(ReceiptMeansState.WrittenOff, "el medio se da de baja sin saldo a favor");
        RecaudoYTesoreriaE2E.Enum<ReceiptMeansState>(medios.Single(m => m.GetProperty("meansCode").GetString() != CodigoDelCheque).GetProperty("status"))
            .Should().Be(ReceiptMeansState.Active);

        // El aviso al pagador va en la alerta de la oficina; el girador queda en la lista que advierte al cajero; el informe de cheques devueltos lo trae.
        var alertas = await CarteraE2E.GetAsync(http, t, "/api/inventory/alerts?typeCode=Recaudo.ChequeDevuelto");
        alertas.ToString().Should().Contain("0004417").And.Contain("Avise al pagador");
        var giradores = (await InventarioE2E.GetAsync(http, t, "/api/receipts/received-checks/drawers-with-returns?account=9988776655")).EnumerateArray().ToList();
        giradores.Should().ContainSingle().Which.GetProperty("returned").GetInt32().Should().Be(1);
        var informe = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, t,
            $"/api/reports/receipts/returned-checks?from={hoy.AddDays(-1):yyyy-MM-dd}&to={hoy.AddDays(1):yyyy-MM-dd}"));
        var filaDelCheque = informe.Filas.Where(f => informe.Texto(f, "Cheque") == "0004417").ToList();
        filaDelCheque.Should().ContainSingle("el informe de cheques devueltos trae el cheque");
        informe.Numero(filaDelCheque[0], "Valor").Should().Be(50_000m);
        informe.Texto(filaDelCheque[0], "Causal").Should().StartWith("01");

        // Devolverlo otra vez no hace nada más.
        var otraVez = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/received-checks/{chequeId}/return", new { causeCode = "01" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Receipts.Check.AlreadyReturned");
    }

    /// <summary>
    /// Segunda ronda del 2026-10-27 (D-l, T0501; FR-235): el cheque pagado a mano (sin lote de canje) dice a qué cuenta propia entró y emite <c>ChequeRecibidoPagado</c>
    /// (<c>ChequesEnCanje</c> → <c>BancoRecaudo</c> de esa cuenta); sin la cuenta, la caja la pide.
    /// </summary>
    [Fact]
    public async Task El_cheque_pagado_pasa_del_canje_al_banco_de_su_cuenta()
    {
        using var http = fx.CreateClient();
        Guid? medioCheque = null;
        var c = await CajaE2E.PreparadaAsync(fx, http, "chpagado", async esc =>
        {
            await RecaudoYTesoreriaE2E.ParametroAsync(fx, esc.Coop, "RCD", "Caja.AdmiteCheque", "true", esc.Hoy, "La cooperativa recibe cheques en ventanilla (e2e)");
            medioCheque = await ConfiguracionDeRecaudoE2E.MedioAsync(http, esc.Coop.TokenAdmin, CodigoDelCheque, "Check", esc.Hoy.AddDays(-1));
            return [medioCheque.Value];
        });
        var t = c.Token;
        var banco = await RecaudoYTesoreriaE2E.BancoAsync(http, t);
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 30_000m)],
            [new { paymentMeansPublicId = medioCheque!.Value, amount = 30_000m, reference = "0005501",
                check = new { draweeBankPublicId = banco, checkNumber = "0005501", accountNumber = "1122334455", issueDate = c.Escenario.Hoy, samePlace = true } }]);
        await CajaE2E.ReciboAsync(http, c, op);
        var chequeId = (await InventarioE2E.GetAsync(http, t, "/api/receipts/received-checks")).EnumerateArray()
            .Single(x => x.GetProperty("checkNumber").GetString() == "0005501").GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/receipts/received-checks/{chequeId}/deposit", new { note = "Consignado (e2e)" });

        var sinCuenta = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/received-checks/{chequeId}/paid", new { note = "Pagado (e2e)" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinCuenta)).Should().Be("Receipts.Check.PaidAccountRequired");
        var cuenta = (await InventarioE2E.GetAsync(http, t, "/api/receipts/transfer-accounts")).EnumerateArray().First().GetProperty("ownBankAccountPublicId").GetGuid();
        var pagado = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/receipts/received-checks/{chequeId}/paid",
            new { note = "Pagado (e2e)", ownBankAccountPublicId = cuenta });
        RecaudoYTesoreriaE2E.Enum<ReceivedCheckState>(pagado.GetProperty("status")).Should().Be(ReceivedCheckState.Paid);
        (await DesembolsoE2E.EnLaBaseAsync(fx, c.Escenario, db => db.IntegrationMessages.AsNoTracking()
            .CountAsync(m => m.Type == IngenIA365ERP.Application.Common.Integration.Contracts.Receipts.ChequeRecibidoPagadoV1.Type && m.OriginPublicId == chequeId)))
            .Should().Be(1, "pagar el cheque emite su hecho una vez");
    }

    /// <summary>Las porciones del recibo de la operación: el código del medio, el valor y el estado (la API no las expone).</summary>
    private Task<List<(string Medio, decimal Valor, PortionState Estado)>> PorcionesAsync(CajaE2E.Caja c, Guid operacion) =>
        DesembolsoE2E.EnLaBaseAsync(fx, c.Escenario, async db =>
            (await (from p in db.ReceiptApplicationPortions.AsNoTracking()
                    join m in db.ReceiptMeans.AsNoTracking() on p.ReceiptMeansId equals m.Id
                    join r in db.IssuedReceipts.AsNoTracking() on m.ReceiptId equals r.Id
                    join pm in db.PaymentMeans.AsNoTracking() on m.PaymentMeansId equals pm.Id
                    where r.PublicId == operacion
                    select new { pm.Code, p.Amount, p.Status }).ToListAsync())
            .Select(x => (x.Code, x.Amount, x.Status)).ToList());

    /// <summary>Los pagos del crédito A que siguen vigentes en Cartera (sin reverso).</summary>
    private async Task<List<string>> PagosVigentesAsync(CajaE2E.Caja c) =>
        (await PagosE2E.OperacionesAsync(fx, c.Escenario, c.CreditoA))
        .Where(o => o.Clase == IngenIA365ERP.Domain.Enums.Lending.LoanOperationKind.Payment && o.ReversadaPor is null).Select(o => o.Numero).ToList();

    /// <summary>
    /// Los aportes de la persona de la caja (recorrido H): la naturaleza de aportes activada sin carga en la cooperativa de la caja —con los requisitos que exige en todo
    /// ambiente—, una línea de aportes que admite extraordinarios y la cuenta de aportes de la persona. Devuelve la cuenta.
    /// </summary>
    private async Task<Guid> CuentaDeAportesAsync(HttpClient http, CajaE2E.Caja c)
    {
        var coop = c.Escenario.Coop;
        var admin = coop.TokenAdmin;
        var hoy = c.Escenario.Hoy;
        await CaptacionesE2E.AlcanceTotalDelAdministradorAsync(fx, coop);
        await CaptacionesE2E.RequisitosDeCaptacionesAsync(http, admin, hoy);
        // El asociado se registra con su autorización de tratamiento de datos, y ésta exige una política vigente de esa finalidad (la cooperativa del desembolso sólo
        // trae la de estudio de crédito).
        await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
        {
            title = "Política de datos: GeneralTreatment", contentMarkdown = "Texto de la política para las e2e de la caja.", effectiveFrom = DateTime.UtcNow.AddMinutes(-5),
            purpose = "GeneralTreatment",
        });
        var activada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/deposits/activation", new
        {
            nature = "Contribution", activationDate = hoy, acknowledgeWarnings = true, reason = "Activación sin carga de aportes (e2e de la caja)",
        });
        if (activada.StatusCode != HttpStatusCode.Created)
            (await CarteraE2E.CodigoDeErrorAsync(activada)).Should().Be("Lending.Activation.AlreadyActivated", $"activar aportes: «{await activada.Content.ReadAsStringAsync()}»");

        var linea = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/lines/contribution", new
        {
            code = "APCAJA", name = "Aportes sociales (caja)", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Estatutos, art. 20 (e2e)", minimumNonReducible = new { kind = "FixedAmount", amount = 0m },
                installment = new { kind = "FixedAmount", amount = 50_000m, periodicity = "Monthly", dueDay = 5 }, allowsExtraordinary = true, refundTermDays = 60,
            },
        });
        linea.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea de aportes: «{await linea.Content.ReadAsStringAsync()}»");
        // La activación de aportes ya ocurrió: con la línea activa, la matriz mínima que pide la cuenta (recaudo por aplicar y aportes ordinarios).
        await CaptacionesE2E.RequisitosDeCaptacionesAsync(http, admin, hoy);
        await OriginacionE2E.AutorizarAsync(http, admin, c.Persona, "GeneralTreatment");
        // La persona del crédito nace sin fecha de nacimiento y abrir una cuenta exige los datos de conocimiento (Lending.Savings.MemberDataIncomplete).
        var actual = await CarteraE2E.GetAsync(http, admin, $"/api/core/people/{c.Persona}");
        var persona = actual.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
        persona["dateOfBirth"] = new DateOnly(1984, 5, 20);
        var editada = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/core/people/{c.Persona}", persona);
        editada.IsSuccessStatusCode.Should().BeTrue($"completar la fecha de nacimiento: «{await editada.Content.ReadAsStringAsync()}»");
        var cuenta = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/accounts", new
        {
            linePublicId = (await CarteraE2E.LeerAsync(linea)).GetProperty("publicId").GetGuid(), holderPersonPublicId = c.Persona,
            officePublicId = c.Escenario.Usuarios.O1.AgenciaPublicId, handlingMode = "Indistinct", parties = Array.Empty<object>(), beneficiaries = Array.Empty<object>(),
            markGmfExempt = false,
        });
        cuenta.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la cuenta de aportes: «{await cuenta.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(cuenta)).GetProperty("publicId").GetGuid();
    }
}
