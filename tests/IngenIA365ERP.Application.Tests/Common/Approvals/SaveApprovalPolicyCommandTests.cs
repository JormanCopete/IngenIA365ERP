using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.ListApprovalPolicies;
using IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Approvals;

/// <summary>
/// T015 (feature 012; T33; contracts/api.md §15.1, §15.4; data-model §21): el alta de una versión de política de
/// aprobación y su consulta. InMemory con el catálogo de permisos sembrado y las reglas del módulo falsas.
/// </summary>
public class SaveApprovalPolicyCommandTests
{
    private const string Supervisor = "Inventory.Approvals.Supervisor";
    private const string Gerencia = "Inventory.Approvals.Management";

    private static readonly DateOnly Octubre = new(2026, 10, 1);
    private static readonly Guid TipoAjuste = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private IReglasDePoliticaDeAprobacion _reglas = new ReglasDePoliticaDeAprobacionVacias();

    public SaveApprovalPolicyCommandTests()
    {
        _db.Permissions.AddRange(
            new Permission { Resource = "Inventory.Approvals", Action = "Supervisor" },
            new Permission { Resource = "Inventory.Approvals", Action = "Management" },
            new Permission { Resource = "Inventory.Adjustments", Action = "Approve" });
        _db.SaveChanges();
    }

    private Task<Result<ApprovalPolicyDto>> Enviar(SaveApprovalPolicyCommand c) =>
        new SaveApprovalPolicyCommandHandler(_db, _reglas).Handle(c, CancellationToken.None);

    private static SaveApprovalPolicyCommand Alta(DateOnly desde, Guid? tipo = null, string subject = ApprovalSubjects.DocumentConfirmation,
        string reason = "Acta 7 del consejo", params (int Orden, decimal Umbral, string Permiso)[] niveles) =>
        new(subject, tipo, desde, reason, niveles.Select(n => new NivelDto(n.Orden, n.Umbral, n.Permiso)).ToList()) { OperationKey = Guid.NewGuid() };

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    [Fact]
    public async Task Registra_la_primera_version_con_sus_niveles()
    {
        var r = await Enviar(Alta(Octubre, niveles: [(1, 0m, Supervisor), (2, 1_000_000m, Gerencia)]));

        r.IsSuccess.Should().BeTrue();
        r.Value.Version.Should().Be(1);
        r.Value.Module.Should().Be("Inventory");
        r.Value.DocumentType.Should().BeNull();
        r.Value.Levels.Select(l => (l.Order, l.Threshold, l.PermissionCode)).Should().Equal((1, 0m, Supervisor), (2, 1_000_000m, Gerencia));
        var p = await _db.ApprovalPolicies.Include(x => x.Levels).SingleAsync();
        p.PolicyKey.Should().Be("Inventory|DocumentConfirmation|*");
        p.Levels.Should().HaveCount(2);
    }

    [Fact]
    public async Task Una_version_nueva_cierra_la_anterior_la_vispera_y_suma_uno()
    {
        await Enviar(Alta(Octubre, niveles: [(1, 0m, Supervisor)]));

        var r = await Enviar(Alta(new DateOnly(2026, 11, 15), niveles: [(1, 0m, Gerencia)]));

        r.Value.Version.Should().Be(2);
        var versiones = await _db.ApprovalPolicies.OrderBy(p => p.ValidFrom).ToListAsync();
        versiones[0].ValidTo.Should().Be(new DateOnly(2026, 11, 14));
        versiones[1].ValidTo.Should().BeNull();
    }

    [Fact]
    public async Task Niveles_vacios_es_una_version_sin_aprobacion()
    {
        await Enviar(Alta(Octubre, niveles: [(1, 0m, Supervisor)]));

        var r = await Enviar(Alta(new DateOnly(2026, 12, 1)));

        r.IsSuccess.Should().BeTrue();
        r.Value.Levels.Should().BeEmpty();
    }

    [Theory]
    [InlineData("2026-10-01")]
    [InlineData("2026-09-01")]
    public async Task Una_version_posterior_o_del_mismo_dia_es_Overlaps(string desde)
    {
        await Enviar(Alta(Octubre, niveles: [(1, 0m, Supervisor)]));

        var r = await Enviar(Alta(DateOnly.Parse(desde), niveles: [(1, 0m, Gerencia)]));

        r.Error.Code.Should().Be("Approvals.Policy.Overlaps");
        Datos(r.Error).GetProperty("existingValidFrom").GetString().Should().Be("2026-10-01");
        (await _db.ApprovalPolicies.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task La_del_tipo_y_la_de_todos_son_series_distintas()
    {
        _reglas = Substitute.For<IReglasDePoliticaDeAprobacion>();
        _reglas.EvaluarAsync(Arg.Any<AltaDePoliticaDeAprobacion>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _reglas.DescribirTiposAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, TipoDeDocumentoDeAprobacionDto> { [TipoAjuste] = new(TipoAjuste, "AJ", "Ajuste positivo", "PositiveAdjustment") });
        await Enviar(Alta(Octubre, niveles: [(1, 0m, Supervisor)]));

        var r = await Enviar(Alta(Octubre, TipoAjuste, niveles: [(1, 0m, Gerencia)]));

        r.IsSuccess.Should().BeTrue();
        r.Value.Version.Should().Be(1);
        r.Value.DocumentType!.Code.Should().Be("AJ");
    }

    [Fact]
    public async Task Sin_modulo_que_conozca_el_tipo_de_documento_no_existe()
    {
        var r = await Enviar(Alta(Octubre, TipoAjuste, niveles: [(1, 0m, Supervisor)]));

        r.Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Las_reglas_del_modulo_pueden_rechazar()
    {
        _reglas = Substitute.For<IReglasDePoliticaDeAprobacion>();
        _reglas.EvaluarAsync(Arg.Any<AltaDePoliticaDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new ErrorConDatos("Approvals.Policy.RequiredForClass", "El saldo inicial siempre se aprueba.", new { @class = "OpeningBalance" })));

        var r = await Enviar(Alta(Octubre, TipoAjuste));

        r.Error.Code.Should().Be("Approvals.Policy.RequiredForClass");
        await _reglas.Received().EvaluarAsync(new AltaDePoliticaDeAprobacion(ApprovalSubjects.DocumentConfirmation, TipoAjuste, Octubre, 0), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("1:0,3:100", "orden")]
    [InlineData("2:0", "orden")]
    [InlineData("1:100,2:50", "umbral")]
    [InlineData("1:-1", "umbral")]
    public async Task Niveles_mal_formados_son_LevelsInvalid(string niveles, string motivo)
    {
        var lista = niveles.Split(',').Select(n => n.Split(':')).Select(p => (int.Parse(p[0]), decimal.Parse(p[1]), Supervisor)).ToArray();

        var r = await Enviar(Alta(Octubre, niveles: lista));

        r.Error.Code.Should().Be("Approvals.Policy.LevelsInvalid");
        Datos(r.Error).GetProperty("reason").GetString().Should().Contain(motivo);
    }

    [Fact]
    public async Task Un_permiso_fuera_del_catalogo_es_PermissionUnknown()
    {
        var r = await Enviar(Alta(Octubre, niveles: [(1, 0m, Supervisor), (2, 10m, "Inventory.Approvals.Inventado")]));

        r.Error.Code.Should().Be("Approvals.Policy.PermissionUnknown");
        Datos(r.Error).GetProperty("permissionCode").GetString().Should().Be("Inventory.Approvals.Inventado");
        (await _db.ApprovalPolicies.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void El_validador_exige_sujeto_del_catalogo_motivo_y_fecha()
    {
        var v = new SaveApprovalPolicyCommandValidator();

        v.Validate(Alta(Octubre, subject: "Otro")).IsValid.Should().BeFalse("el sujeto es de ApprovalSubjects");
        v.Validate(Alta(Octubre, subject: "documentconfirmation")).IsValid.Should().BeFalse();
        v.Validate(Alta(Octubre, reason: " ")).IsValid.Should().BeFalse("el motivo es obligatorio");
        v.Validate(Alta(Octubre, reason: new string('x', 301))).IsValid.Should().BeFalse("el motivo admite 300");
        v.Validate(Alta(default)).IsValid.Should().BeFalse();
        v.Validate(Alta(Octubre, niveles: [(1, 0.001m, Supervisor)])).IsValid.Should().BeFalse("el umbral es en pesos con 2 decimales");
        foreach (var sujeto in ApprovalSubjects.Todos)
            v.Validate(Alta(Octubre, subject: sujeto, niveles: [(1, 0m, Supervisor)])).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_consulta_devuelve_la_vigente_o_toda_la_historia()
    {
        await Enviar(Alta(Octubre, niveles: [(1, 0m, Supervisor)]));
        await Enviar(Alta(new DateOnly(2026, 11, 1), niveles: [(1, 0m, Gerencia)]));
        var reloj = Substitute.For<IDateTimeService>();
        reloj.HoyLocal.Returns(new DateOnly(2026, 10, 15));
        var consulta = new ListApprovalPoliciesQueryHandler(_db, _reglas, reloj);

        var hoy = await consulta.Handle(new ListApprovalPoliciesQuery(), CancellationToken.None);
        var noviembre = await consulta.Handle(new ListApprovalPoliciesQuery(AsOf: new DateOnly(2026, 11, 20)), CancellationToken.None);
        var historia = await consulta.Handle(new ListApprovalPoliciesQuery(Subject: ApprovalSubjects.DocumentConfirmation, IncludeHistory: true), CancellationToken.None);
        var otroSujeto = await consulta.Handle(new ListApprovalPoliciesQuery(Subject: ApprovalSubjects.DiscountOverCap, IncludeHistory: true), CancellationToken.None);

        hoy.Value.Should().ContainSingle().Which.Levels.Single().PermissionCode.Should().Be(Supervisor);
        noviembre.Value.Should().ContainSingle().Which.Version.Should().Be(2);
        historia.Value.Select(p => p.Version).Should().Equal(1, 2);
        otroSujeto.Value.Should().BeEmpty();
    }
}
