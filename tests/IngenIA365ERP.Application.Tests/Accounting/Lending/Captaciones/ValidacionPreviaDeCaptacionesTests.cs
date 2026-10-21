using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using H = IngenIA365ERP.Application.Lending.Integration.HechosRepresentativosDeCaptaciones;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones;

/// <summary>
/// Feature 014, US8 (T142, T145, T148; contabilidad.md §7 de la 014; research E108): la validación previa de captaciones pregunta a Contabilidad con hechos
/// representativos de cada operación —con las dimensiones de la cuenta, su titular y su número, fecha y origen propios— por el mismo
/// <c>EvaluateLendingPostingQuery</c> de los créditos. Con la matriz completa todo es contabilizable; quitar una regla hace que la pregunta diga cuál falta
/// (quickstart §2, US8: «quitar una regla y ver el bloqueo»).
/// </summary>
public class ValidacionPreviaDeCaptacionesTests
{
    private readonly EscenarioDeCaptaciones S = new();

    private H.CuentaRepresentativa AlaVista => H.Cuenta(S.AlaVista(), S.Titular, "AHVISTA-1");
    private H.CuentaRepresentativa Aportes => H.Cuenta(S.Aportes(), S.Titular, "APORTES-1");

    private async Task<ResultadoDeEvaluacionContableDto> EvaluarAsync(IReadOnlyList<MensajeContableDto> hechos)
    {
        var handler = new EvaluateLendingPostingQueryHandler(S.E.D.Db, S.E.D.Poster, new ResolutorDeReglasDeCartera(S.E.D.Db),
            new TiposDeComprobanteDeCartera(S.E.D.Db), S.E.D.Clock);
        var r = await handler.Handle(new EvaluateLendingPostingQuery(hechos), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    private IReadOnlyList<MensajeContableDto> TodasLasOperaciones()
    {
        var dia = EscenarioDeCaptaciones.Dia;
        var v = AlaVista;
        var a = Aportes;
        return
        [
            .. H.Unidad(dia, v, H.Consignacion(v, "Receipts")),
            .. H.Unidad(dia, a, H.Consignacion(a, "Payroll")),
            .. H.Unidad(dia, v, H.Retiro(v, null), H.Gmf(v)),
            .. H.Unidad(dia, v, H.Retiro(v, "OwnLoanPayoff", interno: true), H.Gmf(v)),
            .. H.Unidad(dia, v, H.Traslado(v, a), H.Gmf(v)),
            .. H.Unidad(dia, v, H.Causacion(v)),
            .. H.Unidad(dia, v, H.Liquidacion(v), H.Retencion(v)),
            .. H.Unidad(dia, v, H.Cargo(v, ChargeKind.MaintenanceFee, cobrado: true)),
            .. H.Unidad(dia, v, H.Cargo(v, ChargeKind.Inactivity, cobrado: false)),
            .. H.Unidad(dia, a, H.Cruce(a)),
            .. H.Unidad(dia, v, H.Cruce(v)),
            .. H.Unidad(dia, a, H.Perdida(a)),
            .. H.Unidad(dia, a, H.Devolucion(a, "MembershipRefund")),
            .. H.Unidad(dia, v, H.Devolucion(v, "MembershipRefund")),
        ];
    }

    [Fact]
    public async Task Con_la_matriz_completa_cada_operacion_de_captaciones_es_contabilizable()
    {
        var r = await EvaluarAsync(TodasLasOperaciones());

        r.Errors.Select(e => $"{e.FactType} {e.Role}: {e.Rule} {e.Message}").Should().BeEmpty();
        r.IsPostable.Should().BeTrue();
        (await S.E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0, "preguntar no contabiliza");
    }

    [Fact]
    public async Task Sin_la_regla_del_GMF_la_pregunta_dice_el_hecho_el_rol_y_que_lo_corrige_Contabilidad()
    {
        S.E.D.Db.LendingPostingRules.RemoveRange(S.E.D.Db.LendingPostingRules.Where(x => x.Role == R.GmfPorPagar));
        await S.E.D.Db.SaveChangesAsync();

        var r = await EvaluarAsync(TodasLasOperaciones());

        r.IsPostable.Should().BeFalse();
        r.Errors.Should().OnlyContain(e => e.Role == R.GmfPorPagar && e.Rule == "Accounting.LendingRule.Missing");
        r.Errors.Should().Contain(e => e.FactType == "GmfRetenido");
        r.Errors.Select(e => e.WhoFixes.Module).Should().OnlyContain(w => w == "Accounting");
    }

    [Fact]
    public async Task Una_pregunta_sin_fecha_ni_origen_se_evalua_hoy_y_como_su_propia_unidad()
    {
        // Así llegaban los representativos hasta el 2026-10-18 (research E108): sin fecha (el año 1, donde ninguna regla rige) y sin origen (todos una unidad).
        var sinFecha = H.Unidad(default, AlaVista, H.Consignacion(AlaVista, "Receipts"))
            .Concat(H.Unidad(default, Aportes, H.Consignacion(Aportes, "Receipts")))
            .Select(m => m with { Envelope = m.Envelope with { Origin = m.Envelope.Origin with { PublicId = Guid.Empty, OperationDate = default } } })
            .ToList();

        var r = await EvaluarAsync(sinFecha);

        r.Errors.Should().NotContain(e => e.Rule == "Accounting.LendingRule.Missing", "la matriz que rige hoy resuelve las dos");
        r.Errors.Should().NotContain(e => e.Rule == "Accounting.LendingFact.Unbalanced", "cada pregunta es su propia unidad y cuadra sola");
    }

    [Fact]
    public void La_clave_de_combinacion_no_cambia_con_el_titular_ni_el_numero_de_la_cuenta()
    {
        var una = H.Unidad(EscenarioDeCaptaciones.Dia, AlaVista with { Titular = Guid.Empty, Numero = H.NumeroDeValidacion },
            H.Liquidacion(AlaVista with { Titular = Guid.Empty, Numero = H.NumeroDeValidacion }));
        var otra = H.Unidad(EscenarioDeCaptaciones.Dia, AlaVista with { Titular = Guid.Empty, Numero = H.NumeroDeValidacion },
            H.Liquidacion(AlaVista with { Titular = Guid.Empty, Numero = H.NumeroDeValidacion }));

        ValidacionPreviaContableDeCartera.ClaveDeCombinacion(una[0]).Should().Be(ValidacionPreviaContableDeCartera.ClaveDeCombinacion(otra[0]));
        var exenta = H.Cuenta(S.AlaVista("GmfExempt"), S.Titular);
        ValidacionPreviaContableDeCartera.ClaveDeCombinacion(H.Unidad(EscenarioDeCaptaciones.Dia, AlaVista, H.Causacion(AlaVista))[0])
            .Should().NotBe(ValidacionPreviaContableDeCartera.ClaveDeCombinacion(H.Unidad(EscenarioDeCaptaciones.Dia, exenta, H.Causacion(exenta))[0]), "la marca sí cambia la combinación");
    }

    [Fact]
    public void Cada_representativo_se_desglosa_en_roles_de_su_hecho()
    {
        foreach (var unidad in TodasLasOperaciones().GroupBy(m => m.Envelope.Origin.PublicId))
        {
            var hechos = unidad.Select(HechoDeCartera.De).ToList();
            var desglose = ConstructorDeLineasDeCartera.Desglosar(hechos);
            desglose.Fallos.Select(f => $"{f.FactType}: {f.Error.Code} {f.Error.Message}").Should().BeEmpty();
            desglose.Partidas.Should().OnlyContain(p => OperacionesDeCartera.Buscar(p.Hecho)!.TieneRol(p.Rol));
        }
    }
}
