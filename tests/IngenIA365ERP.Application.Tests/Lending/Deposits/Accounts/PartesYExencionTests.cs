using FluentAssertions;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.GmfExemption;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.Parties;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;

/// <summary>
/// Feature 014, T077 (US2; api.md §4 y §10; data-model §3.2, §3.8; FR-033, FR-034, FR-036; R22): el cambio de partes (lo que no cambia sigue, lo demás se
/// cierra la víspera y nace hoy; los beneficiarios cambian en conjunto con la constancia), la marca de exención del GMF (una por persona, reemplazarla,
/// terminarla, la política sin marca) y el subpaso de la mayoría de edad con la actualización de datos que levanta su marca. (nuevo)
/// </summary>
public sealed class PartesYExencionTests : IDisposable
{
    private readonly CuentasDePrueba _c = new();

    public void Dispose() => _c.Dispose();

    private static MemberAccountBeneficiaryInput Beneficiario(Domain.Entities.Core.Person p, decimal porcentaje) => new(p.PublicId, porcentaje);

    // -------------------------------------------------------------------------------------------------------- partes --

    [Fact]
    public async Task Cambiar_los_beneficiarios_cierra_el_conjunto_vigente_y_abre_el_siguiente_con_la_constancia()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana, beneficiarios: [Beneficiario(_c.Carla, 60m), Beneficiario(_c.Dario, 40m)]));
        _c.G.HoyLocal = CuentasDePrueba.Hoy.AddDays(10);

        var sinConstancia = await _c.CambiarPartes().Handle(new SetMemberAccountPartiesCommand(cuenta.PublicId, HandlingMode.Indistinct, [],
            [Beneficiario(_c.Carla, 100m)], null, "Pedido del titular"), default);
        sinConstancia.Error.Code.Should().Be("Lending.Savings.BeneficiaryRecordRequired");
        _c.G.Db.ChangeTracker.Clear();

        var constancia = Guid.NewGuid();
        var r = await _c.CambiarPartes().Handle(new SetMemberAccountPartiesCommand(cuenta.PublicId, HandlingMode.Indistinct, [],
            [Beneficiario(_c.Carla, 100m)], constancia, "Pedido del titular"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Beneficiaries.Should().ContainSingle().Which.Should().Match<MemberAccountBeneficiaryDto>(b =>
            b.Person.PersonPublicId == _c.Carla.PublicId && b.Percent == 100m && b.SetNumber == 2 && b.RecordAttachmentPublicId == constancia);
        var beneficiarios = await _c.G.Db.MemberAccountParties.AsNoTracking().Where(p => p.Role == MemberAccountPartyRole.Beneficiary).ToListAsync();
        beneficiarios.Should().HaveCount(3, "nada se borra");
        beneficiarios.Where(p => p.BeneficiarySetNumber == 1).Should().OnlyContain(p => p.ValidTo == CuentasDePrueba.Hoy.AddDays(9));
        (await _c.G.Db.MemberAccountParties.AsNoTracking().SingleAsync(p => p.Role == MemberAccountPartyRole.Holder)).ValidTo
            .Should().BeNull("el titular no cambió");

        // FR-034: en el fallecimiento cuenta el conjunto vigente a la fecha: antes del cambio, el primero.
        var antes = await _c.LeerPartes().Handle(new GetMemberAccountPartiesQuery(cuenta.PublicId, CuentasDePrueba.Hoy.AddDays(5)), default);
        antes.Value.Beneficiaries.Select(b => b.Percent).Should().BeEquivalentTo([60m, 40m]);
    }

    [Fact]
    public async Task Un_firmante_con_limite_nace_hoy_y_quitarlo_lo_cierra_y_el_manejo_cambia()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana));
        var firmante = new MemberAccountPartyInput { PersonPublicId = _c.Beto.PublicId, Role = MemberAccountPartyRole.Signatory, SignatureLimit = 2_000_000m };

        var con = await _c.CambiarPartes().Handle(new SetMemberAccountPartiesCommand(cuenta.PublicId, HandlingMode.Joint, [firmante], [], null, "Firma conjunta"),
            default);
        con.IsSuccess.Should().BeTrue(con.IsFailure ? $"{con.Error.Code}: {con.Error.Message}" : string.Empty);
        con.Value.HandlingMode.Should().Be(HandlingMode.Joint);
        con.Value.Parties.Single(p => p.Role == MemberAccountPartyRole.Signatory).SignatureLimit.Should().Be(2_000_000m);
        _c.G.Db.ChangeTracker.Clear();

        _c.G.HoyLocal = CuentasDePrueba.Hoy.AddDays(3);
        var sin = await _c.CambiarPartes().Handle(new SetMemberAccountPartiesCommand(cuenta.PublicId, HandlingMode.Indistinct, [], [], null, "Revocado"), default);
        sin.Value.Parties.Should().ContainSingle(p => p.Role == MemberAccountPartyRole.Holder);
        var historia = await _c.LeerPartes().Handle(new GetMemberAccountPartiesQuery(cuenta.PublicId, IncludeHistory: true), default);
        historia.Value.History.Single(p => p.Role == MemberAccountPartyRole.Signatory).Should().Match<MemberAccountPartyDto>(p =>
            p.ValidTo == CuentasDePrueba.Hoy.AddDays(2) && p.EndReason!.Contains("Revocado"));
    }

    [Fact]
    public async Task Cambiar_partes_el_mismo_dia_de_la_apertura_deja_la_fila_anterior_sin_vigencia_y_no_la_borra()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana, beneficiarios: [Beneficiario(_c.Carla, 100m)]));

        var r = await _c.CambiarPartes().Handle(new SetMemberAccountPartiesCommand(cuenta.PublicId, HandlingMode.Indistinct, [],
            [Beneficiario(_c.Dario, 100m)], Guid.NewGuid(), "Corrección el mismo día"), default);

        r.Value.Beneficiaries.Should().ContainSingle().Which.Person.PersonPublicId.Should().Be(_c.Dario.PublicId);
        var anterior = await _c.G.Db.MemberAccountParties.AsNoTracking().SingleAsync(p => p.PersonId == _c.Carla.Id);
        anterior.ValidTo.Should().Be(CuentasDePrueba.Hoy.AddDays(-1));
        anterior.VigenteEn(CuentasDePrueba.Hoy).Should().BeFalse();
    }

    [Fact]
    public async Task Quitar_el_representante_de_un_menor_se_rechaza()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var menor = _c.Persona("Sofia", "Menor", "1010", new DateOnly(2015, 5, 20), asociada: true);
        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, menor, [Representante(_c.Ana)]));

        var r = await _c.CambiarPartes().Handle(new SetMemberAccountPartiesCommand(cuenta.PublicId, HandlingMode.Indistinct, [], [], null, "Quitar"), default);

        r.Error.Code.Should().Be("Lending.Savings.RepresentativeRequired");
    }

    private static MemberAccountPartyInput Representante(Domain.Entities.Core.Person p) => new()
    {
        PersonPublicId = p.PublicId, Role = MemberAccountPartyRole.LegalRepresentative, RepresentationBasis = "Parent", SupportAttachmentPublicId = Guid.NewGuid(),
    };

    // ------------------------------------------------------------------------------------------------- mayoría de edad --

    [Fact]
    public async Task Al_cumplir_18_el_proceso_cierra_el_representante_la_vispera_marca_actualizar_datos_y_no_lo_repite()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var menor = _c.Persona("Sofia", "Casi", "1011", new DateOnly(2008, 12, 15), asociada: true);
        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, menor, [Representante(_c.Ana)]));
        var id = (await _c.G.Db.MemberAccounts.SingleAsync()).Id;
        var corrida = new LendingProcessRun { Id = 1 };

        (await _c.Mayoria().ProcesarAsync(corrida, new DateOnly(2026, 12, 14), [id], default)).Should().BeEmpty();
        (await _c.G.Db.MemberAccountParties.AsNoTracking().SingleAsync(p => p.Role == MemberAccountPartyRole.LegalRepresentative)).ValidTo
            .Should().BeNull("la víspera todavía es menor");

        (await _c.Mayoria().ProcesarAsync(corrida, new DateOnly(2026, 12, 15), [id], default)).Should().BeEmpty();
        _c.G.Db.ChangeTracker.Clear();
        var representante = await _c.G.Db.MemberAccountParties.AsNoTracking().SingleAsync(p => p.Role == MemberAccountPartyRole.LegalRepresentative);
        representante.ValidTo.Should().Be(new DateOnly(2026, 12, 14));
        representante.EndReason.Should().Be(Application.Lending.Deposits.Accrual.MayoriaDeEdad.MotivoDeFin);
        representante.EndedByUserId.Should().BeNull("lo hace el proceso");
        var guardada = await _c.G.Db.MemberAccounts.AsNoTracking().SingleAsync();
        guardada.RequiresDataUpdate.Should().BeTrue();
        guardada.DataUpdateRequiredSince.Should().Be(new DateOnly(2026, 12, 15));
        guardada.Number.Should().Be(cuenta.Number, "el número no cambia");

        (await _c.Mayoria().CandidatasAsync([id], new DateOnly(2026, 12, 20), 18, default)).Should().BeEmpty("ya no tiene representante vigente");

        _c.G.HoyLocal = new DateOnly(2026, 12, 16);
        var r = await _c.ConfirmarDatos().Handle(new ConfirmMemberAccountDataUpdateCommand(cuenta.PublicId, "Actualizó su conocimiento"), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Marks.RequiresDataUpdate.Should().BeFalse();
        _c.G.Db.ChangeTracker.Clear();
        (await _c.ConfirmarDatos().Handle(new ConfirmMemberAccountDataUpdateCommand(cuenta.PublicId, "Otra vez"), default)).Error.Code
            .Should().Be("Lending.Savings.DataUpdateNotRequired");
    }

    [Fact]
    public async Task La_mayoria_de_edad_es_parametro_con_vigencia()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var menor = _c.Persona("Sofia", "Casi", "1011", new DateOnly(2006, 12, 15), asociada: true);
        _c.G.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.CaptacionesMayoriaDeEdadAnios, ScopeKind = ParameterScopeKind.None, Value = "21",
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedAt = _c.G.UtcNow.AddDays(-10),
        });
        await _c.G.Db.SaveChangesAsync();

        var cuenta = await _c.AbrirAsync(_c.Apertura(linea, menor));

        cuenta.Error.Code.Should().Be("Lending.Savings.RepresentativeRequired", "con 21 años de mayoría, a los 19 todavía es menor");
    }

    // ---------------------------------------------------------------------------------------------------------- GMF --

    [Fact]
    public async Task Marcar_otra_cuenta_se_rechaza_y_reemplazar_cierra_la_vigente_la_vispera()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var primera = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana, exenta: true));
        var segunda = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana));

        var r = await _c.Marcar().Handle(new MarkGmfExemptAccountCommand(segunda.PublicId, null, "La pide el titular"), default);
        r.Error.Code.Should().Be("Lending.Gmf.AlreadyMarked");
        GobiernoDePrueba.Datos(r.Error).GetProperty("accountPublicId").GetGuid().Should().Be(primera.PublicId);
        _c.G.Db.ChangeTracker.Clear();

        var manana = CuentasDePrueba.Hoy.AddDays(1);
        var reemplazo = await _c.Marcar().Handle(new MarkGmfExemptAccountCommand(segunda.PublicId, manana, "Cambio de cuenta exenta", ReplaceCurrent: true),
            default);
        reemplazo.IsSuccess.Should().BeTrue(reemplazo.IsFailure ? reemplazo.Error.Message : string.Empty);
        reemplazo.Value.AccountNumber.Should().Be(segunda.Number);
        reemplazo.Value.ValidFrom.Should().Be(manana);
        var marcas = await _c.G.Db.GmfExemptAccounts.AsNoTracking().OrderBy(g => g.Id).ToListAsync();
        marcas.Should().HaveCount(2);
        marcas[0].ValidTo.Should().Be(CuentasDePrueba.Hoy);
        marcas[0].EndReason.Should().Contain(segunda.Number);
        marcas[1].ValidTo.Should().BeNull();
    }

    [Fact]
    public async Task Terminar_una_marca_la_cierra_y_no_se_termina_dos_veces_ni_hacia_atras()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana, exenta: true));
        var marca = await _c.G.Db.GmfExemptAccounts.AsNoTracking().SingleAsync();

        (await _c.Desmarcar().Handle(new EndGmfExemptAccountCommand(marca.PublicId, CuentasDePrueba.Hoy.AddDays(-5), "Atrás"), default)).Error.Code
            .Should().Be("Lending.Gmf.Retroactive");
        var r = await _c.Desmarcar().Handle(new EndGmfExemptAccountCommand(marca.PublicId, null, "Lo pide el titular"), default);
        r.Value.ValidTo.Should().Be(CuentasDePrueba.Hoy);
        _c.G.Db.ChangeTracker.Clear();
        (await _c.Desmarcar().Handle(new EndGmfExemptAccountCommand(marca.PublicId, null, "Otra vez"), default)).Error.Code
            .Should().Be("Lending.Gmf.AlreadyEnded");
    }

    [Fact]
    public async Task Con_la_politica_de_tope_por_titular_no_hay_marca()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana));
        _c.G.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.GmfPoliticaDeExencion, ScopeKind = ParameterScopeKind.None, Value = "PerHolderLimit",
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedAt = _c.G.UtcNow.AddDays(-10),
        });
        await _c.G.Db.SaveChangesAsync();

        var r = await _c.Marcar().Handle(new MarkGmfExemptAccountCommand(cuenta.PublicId, null, "La pide el titular"), default);
        var alAbrir = await _c.AbrirAsync(_c.Apertura(linea, _c.Beto, exenta: true));

        r.Error.Code.Should().Be("Lending.Gmf.PolicyWithoutMark");
        alAbrir.Error.Code.Should().Be("Lending.Gmf.PolicyWithoutMark");
    }

    [Fact]
    public async Task Ni_hacia_atras_ni_en_una_cuenta_de_aportes()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var aportes = await _c.LineaDeAportesAsync();
        var ahorro = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana));
        var deAportes = await _c.AbiertaAsync(_c.Apertura(aportes, _c.Ana));

        (await _c.Marcar().Handle(new MarkGmfExemptAccountCommand(ahorro.PublicId, CuentasDePrueba.Hoy.AddDays(-1), "Atrás"), default)).Error.Code
            .Should().Be("Lending.Gmf.Retroactive");
        (await _c.Marcar().Handle(new MarkGmfExemptAccountCommand(deAportes.PublicId, null, "Aportes"), default)).Error.Code
            .Should().Be("Lending.Gmf.AccountNotSavings");
    }

    [Fact]
    public void Los_validadores_piden_motivo()
    {
        new MarkGmfExemptAccountCommandValidator().Validate(new MarkGmfExemptAccountCommand(Guid.NewGuid(), null, " ")).IsValid.Should().BeFalse();
        new SetMemberAccountPartiesCommandValidator().Validate(new SetMemberAccountPartiesCommand(Guid.NewGuid(), HandlingMode.Indistinct, [], [], null, ""))
            .IsValid.Should().BeFalse();
    }
}
