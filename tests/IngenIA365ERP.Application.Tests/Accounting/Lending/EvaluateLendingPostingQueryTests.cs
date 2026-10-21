using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0619 (T0649; contracts/contabilidad.md §13.1 y §13.3; FR-127): «¿es contabilizable?». Usa el mismo constructor del consumidor,
/// somete los comprobantes a <c>AccountingPoster.ValidarVariosAsync</c>, no numera ni deja entidades rastreadas, reduce los hallazgos a uno por
/// hecho, rol y combinación, dice quién corrige cada uno (§13.3) y sólo evalúa lo que pasaría a Contabilidad.
/// </summary>
public class EvaluateLendingPostingQueryTests
{
    private readonly EscenarioContableDeCartera E = new();

    private async Task<ResultadoDeEvaluacionContableDto> EvaluarAsync(params IReadOnlyList<MensajeEntrante>[] unidades)
    {
        var handler = new EvaluateLendingPostingQueryHandler(E.D.Db, E.D.Poster, new ResolutorDeReglasDeCartera(E.D.Db), new TiposDeComprobanteDeCartera(E.D.Db), E.D.Clock);
        var r = await handler.Handle(new EvaluateLendingPostingQuery(unidades.SelectMany(u => u).Select(m => new MensajeContableDto(m.Envelope, m.Payload!)).ToList()), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    private static IReadOnlyList<MensajeEntrante> Caso(string nombre, Action<JsonObject>? cambio = null)
    {
        var caso = EscenarioContableDeCartera.Caso(nombre);
        cambio?.Invoke(caso);
        return EscenarioContableDeCartera.Unidad(caso);
    }

    private static void CategoriaB(JsonObject c) => c["mensajes"]![0]!["payload"]!["dimensions"]!["riskCategory"] = "B";

    [Fact]
    public async Task Lo_que_tiene_regla_y_pasa_la_009_es_contabilizable_sin_numerar_ni_dejar_rastro()
    {
        var numeroAntes = await E.D.Db.VoucherTypes.Where(v => v.Code == "RC").Select(v => v.NextNumber).SingleAsync();

        var r = await EvaluarAsync(Caso("05-pago-con-anticipo-suspenso-y-saldo-a-favor-en-un-RC"), Caso("03-causacion-del-dia"));

        r.IsPostable.Should().BeTrue();
        r.Errors.Should().BeEmpty();
        E.D.Db.ChangeTracker.Entries().Should().NotContain(x => x.State == EntityState.Added || x.State == EntityState.Modified);
        (await E.D.Db.VoucherTypes.Where(v => v.Code == "RC").Select(v => v.NextNumber).SingleAsync()).Should().Be(numeroAntes, "no numera");
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_regla_dice_el_hecho_el_rol_la_combinacion_y_quien_corrige()
    {
        var r = await EvaluarAsync(Caso("01-desembolso-por-transferencia", CategoriaB));

        r.IsPostable.Should().BeFalse();
        var h = r.Errors.Should().ContainSingle().Subject;
        h.FactType.Should().Be("CreditoDesembolsado");
        h.Role.Should().Be(RolesDeCuentaDeCartera.Capital);
        h.Rule.Should().Be("Accounting.LendingRule.Missing");
        h.Combination.Should().Contain("riskCategory", "B").And.Contain("modalityCode", "CONSUMO");
        h.WhoFixes.Should().Be(new QuienCorrigeDto("Accounting", "/contabilidad/cartera/matriz", "Accounting.LendingRules.Manage"));
    }

    [Fact]
    public async Task Una_pregunta_por_combinacion_distinta()
    {
        var a = Caso("01-desembolso-por-transferencia", CategoriaB);
        var b = EscenarioContableDeCartera.Copia(a);

        var r = await EvaluarAsync(a, b);

        r.Errors.Should().ContainSingle("dos desembolsos con la misma combinación sin regla son un solo hallazgo");
    }

    [Fact]
    public async Task Las_reglas_de_la_009_vuelven_con_quien_las_corrige()
    {
        var cuenta = E.Cuenta("14410501");
        cuenta.IsActive = false;
        await E.D.Db.SaveChangesAsync();

        var r = await EvaluarAsync(Caso("01-desembolso-por-transferencia"));

        var h = r.Errors.Should().ContainSingle().Subject;
        h.Rule.Should().Be("Accounting.Line.AccountInactive");
        h.AccountCode.Should().Be("14410501");
        h.Role.Should().Be(RolesDeCuentaDeCartera.Capital);
        h.WhoFixes.Page.Should().Be("/contabilidad/plan-de-cuentas");
    }

    [Fact]
    public async Task Mas_de_dos_decimales_es_hallazgo_del_emisor()
    {
        var r = await EvaluarAsync(Caso("01-desembolso-por-transferencia", c => c["mensajes"]![0]!["payload"]!["amounts"]![0]!["amount"] = 1.005m));

        var h = r.Errors.Should().ContainSingle().Subject;
        h.Rule.Should().Be("Accounting.LendingFact.Unbalanced");
        h.WhoFixes.Module.Should().Be("Lending", "lo corrige soporte: es defecto del emisor");
    }

    [Fact]
    public async Task No_evalua_informativos_ni_mensajes_de_otros_modulos()
    {
        var informativo = Caso("45-cartera-activada-informativo");
        var deInventario = Caso("01-desembolso-por-transferencia", CategoriaB)
            .Select(m => m with { Envelope = m.Envelope with { OriginModule = "INV" } }).ToList();

        var r = await EvaluarAsync(informativo, deInventario);

        r.IsPostable.Should().BeTrue();
        r.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_contenido_que_llega_como_JSON_se_lee_con_su_record()
    {
        var unidad = Caso("01-desembolso-por-transferencia", CategoriaB);
        var json = JsonSerializer.SerializeToElement(unidad[0].Payload!, OpcionesDeMensajes.Opciones);
        var handler = new EvaluateLendingPostingQueryHandler(E.D.Db, E.D.Poster, new ResolutorDeReglasDeCartera(E.D.Db), new TiposDeComprobanteDeCartera(E.D.Db), E.D.Clock);

        var r = await handler.Handle(new EvaluateLendingPostingQuery([new MensajeContableDto(unidad[0].Envelope, json)]), default);

        r.Value.Errors.Should().ContainSingle().Which.Rule.Should().Be("Accounting.LendingRule.Missing");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_no_hay_que_evaluar()
    {
        var setup = await E.D.Db.AccountingSetups.SingleAsync();
        setup.IsDeleted = true;
        await E.D.Db.SaveChangesAsync();

        var r = await EvaluarAsync(Caso("01-desembolso-por-transferencia", CategoriaB));

        r.IsPostable.Should().BeTrue("sin contabilidad iniciada los hechos de Cartera no pasan (§10.1)");
    }
}
