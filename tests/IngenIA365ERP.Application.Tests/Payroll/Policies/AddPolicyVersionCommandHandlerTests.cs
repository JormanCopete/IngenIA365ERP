using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Policies;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Policies;

/// <summary>
/// T031 (feature 010, R4; contracts/api.md §10.1): políticas por empresa con vigencia. Una
/// vigencia nueva cierra la anterior si se lo piden, no se cruza con otra, sólo admite los
/// valores del catálogo, avisa la retroactividad y la bloquea en la exoneración.
/// </summary>
public class AddPolicyVersionCommandHandlerTests
{
    private readonly NominaTestData _d = new();

    private AddPolicyVersionCommandHandler Handler() => new(_d.Db, _d.Clock, _d.User, _d.StaleMarker, _d.AuditEmitter);

    private CompanyPolicy Vigencia(string clave, string valor, DateOnly desde, DateOnly? hasta = null)
    {
        var p = new CompanyPolicy { Key = clave, Value = valor, ValidFrom = desde, ValidTo = hasta, CreatedBy = "seed" };
        _d.Db.CompanyPolicies.Add(p);
        _d.Db.SaveChanges();
        return p;
    }

    [Fact]
    public async Task Una_vigencia_nueva_cierra_la_anterior_el_dia_antes_y_deja_el_motivo()
    {
        Vigencia(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesASabado, new DateOnly(2026, 1, 1));
        var run = _d.Borrador(_d.Marzo);

        var r = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes,
            new DateOnly(2026, 7, 1), null, "La asamblea aprobó la jornada de lunes a viernes", ClosePrevious: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Warnings.Should().BeEmpty();
        var versiones = await _d.Db.CompanyPolicies.Where(p => p.Key == CompanyPolicyKeys.SemanaLaboral).OrderBy(p => p.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].ValidTo.Should().Be(new DateOnly(2026, 6, 30));
        versiones[1].Value.Should().Be(CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes);
        versiones[1].Notes.Should().Be("La asamblea aprobó la jornada de lunes a viernes");
        (await _d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Stale, "el borrador leyó el valor viejo");
        await _d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollCompanyPolicyChanged && e.OldValuesJson != null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_cerrar_la_anterior_una_vigencia_que_se_cruza_se_rechaza()
    {
        Vigencia(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesASabado, new DateOnly(2026, 1, 1));

        var r = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes,
            new DateOnly(2026, 7, 1), null, "x", ClosePrevious: false), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Payroll.CompanyPolicy.VersionOverlaps");
        (await _d.Db.CompanyPolicies.CountAsync(p => p.Key == CompanyPolicyKeys.SemanaLaboral)).Should().Be(1);
    }

    [Fact]
    public async Task Una_vigencia_futura_dentro_del_rango_nuevo_se_cruza_aunque_se_pida_cerrar()
    {
        Vigencia(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesASabado, new DateOnly(2027, 1, 1));

        var r = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes,
            new DateOnly(2026, 7, 1), null, "x", ClosePrevious: true), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.CompanyPolicy.VersionOverlaps");
    }

    [Fact]
    public async Task Una_clave_desconocida_y_un_valor_fuera_del_catalogo_se_rechazan_nombrando_los_admitidos()
    {
        var desconocida = await Handler().Handle(new AddPolicyVersionCommand("Inventada", "true", new DateOnly(2026, 7, 1), null, "x"), CancellationToken.None);
        desconocida.Error.Code.Should().Be("Payroll.CompanyPolicy.KeyUnknown");

        var invalido = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.SemanaLaboral, "LunesADomingo", new DateOnly(2026, 7, 1), null, "x"), CancellationToken.None);
        invalido.Error.Code.Should().Be("Payroll.CompanyPolicy.ValueInvalid");
        invalido.Error.Message.Should().Contain("LunesASabado").And.Contain("LunesAViernes");
        invalido.Error.Should().BeOfType<ErrorConDatos>();
    }

    [Fact]
    public async Task Las_claves_de_texto_libre_exigen_su_formato()
    {
        var fechaMala = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.ArranqueNominaFecha, "01/12/2026", new DateOnly(2026, 7, 1), null, "x"), CancellationToken.None);
        fechaMala.Error.Code.Should().Be("Payroll.CompanyPolicy.ValueInvalid");

        var fechaBuena = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.ArranqueNominaFecha, "2026-12-01", new DateOnly(2026, 7, 1), null, "Arranque COOFLOPAL"), CancellationToken.None);
        fechaBuena.IsSuccess.Should().BeTrue(fechaBuena.Error.Message);

        var mapaMalo = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.DianMedioPagoMapa, """{ "Transfer": "47" }""", new DateOnly(2026, 7, 1), null, "x"), CancellationToken.None);
        mapaMalo.Error.Code.Should().Be("Payroll.CompanyPolicy.ValueInvalid");

        var mapaBueno = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.DianMedioPagoMapa, """{ "Transfer": "42", "Check": "20", "Cash": "10" }""", new DateOnly(2026, 7, 1), null, "Consignación en cuenta"), CancellationToken.None);
        mapaBueno.IsSuccess.Should().BeTrue(mapaBueno.Error.Message);
    }

    private async Task AprobarMarzoAsync()
    {
        var run = _d.Borrador(_d.Marzo);
        run.Status = PayrollRunStatus.Approved;
        await _d.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Una_vigencia_anterior_a_una_corrida_aprobada_se_registra_con_aviso()
    {
        await AprobarMarzoAsync();

        var r = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes,
            new DateOnly(2026, 3, 1), null, "x"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Warnings.Should().ContainSingle().Which.Should().Contain("31/03/2026");
    }

    [Fact]
    public async Task La_exoneracion_retroactiva_se_bloquea_porque_cambia_aportes_contabilizados()
    {
        await AprobarMarzoAsync();

        var r = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.Exonerada114_1, CompanyPolicyKeys.Verdadero, new DateOnly(2026, 3, 1), null, "x"), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Payroll.CompanyPolicy.RetroactiveNotAllowed");

        var posterior = await Handler().Handle(new AddPolicyVersionCommand(CompanyPolicyKeys.Exonerada114_1, CompanyPolicyKeys.Verdadero, new DateOnly(2026, 4, 1), null, "Concepto de la contadora"), CancellationToken.None);
        posterior.IsSuccess.Should().BeTrue(posterior.Error.Message);
    }

    [Fact]
    public async Task El_listado_trae_las_once_claves_con_defecto_o_vigencia_y_el_historial_ordena_por_fecha()
    {
        Vigencia(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        Vigencia(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesASabado, new DateOnly(2026, 7, 1));

        var lista = await new ListCompanyPoliciesQueryHandler(_d.Db, _d.Clock).Handle(new ListCompanyPoliciesQuery(new DateOnly(2026, 8, 1)), CancellationToken.None);
        lista.IsSuccess.Should().BeTrue();
        lista.Value.Should().HaveCount(CompanyPolicyKeys.Todas.Count);
        var semana = lista.Value.Single(p => p.Key == CompanyPolicyKeys.SemanaLaboral);
        semana.Value.Should().Be(CompanyPolicyKeys.SemanaLaboralValores.LunesASabado);
        semana.VersionCount.Should().Be(2);
        semana.Source.Should().StartWith(ListCompanyPoliciesQueryHandler.FuenteRegistrada);
        var exonerada = lista.Value.Single(p => p.Key == CompanyPolicyKeys.Exonerada114_1);
        exonerada.Value.Should().Be(CompanyPolicyKeys.Falso, "sin vigencia registrada manda el defecto del catálogo");
        exonerada.Source.Should().Be(ListCompanyPoliciesQueryHandler.FuenteDefecto);
        exonerada.Allowed.Should().BeEquivalentTo([CompanyPolicyKeys.Verdadero, CompanyPolicyKeys.Falso]);

        var historial = await new GetPolicyVersionsQueryHandler(_d.Db, _d.Clock).Handle(new GetPolicyVersionsQuery(CompanyPolicyKeys.SemanaLaboral, new DateOnly(2026, 8, 1)), CancellationToken.None);
        historial.Value.Should().HaveCount(2);
        historial.Value[0].ValidFrom.Should().Be(new DateOnly(2026, 7, 1));
        historial.Value[0].IsCurrent.Should().BeTrue();

        var desconocida = await new GetPolicyVersionsQueryHandler(_d.Db, _d.Clock).Handle(new GetPolicyVersionsQuery("Inventada"), CancellationToken.None);
        desconocida.Error.Code.Should().Be("Payroll.CompanyPolicy.KeyUnknown");
    }
}
