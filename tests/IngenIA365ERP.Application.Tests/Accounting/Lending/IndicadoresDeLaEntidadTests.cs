using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0621 (T0649; contracts/contabilidad.md §15; T59, C33, C78; dueño: pregunta C3, rige registrar con soporte y vigencia de 6
/// meses): <c>GetEntityIndicatorsQuery</c> y <c>RegisterRegulatoryIndicatorCommand</c>. El resultado del ejercicio es el saldo de las clases 4 a 7
/// del año sin el comprobante <c>CI</c>; las pérdidas, las cuentas de clase 3 de naturaleza débito, elegidas sin códigos literales; patrimonio
/// técnico y solvencia, el último no reemplazado con corte ≤ fecha, devuelto con su corte sin juzgar la vigencia. Registrar exige soporte de tipo
/// <c>RegulatoryIndicatorSupport</c>; otro del mismo corte lo reemplaza sólo nombrándolo; queda el evento <c>Accounting.RegulatoryIndicator.Registered</c>.
/// </summary>
public class IndicadoresDeLaEntidadTests
{
    private static readonly DateOnly Fecha = new(2026, 3, 20);
    private readonly EscenarioContableDeCartera E = new();

    private ContabilidadTestData D => E.D;

    private async Task ContabilizarAsync(DateOnly fecha, ChartOfAccount debito, ChartOfAccount credito, decimal valor, DocumentKind kind = DocumentKind.Regular, string tipo = "CG")
    {
        var r = await D.Poster.PrepareAsync(new PostingRequest(tipo, fecha, "Prueba", ContabilidadTestData.Manual(),
            [PostingLine.Debito(debito.Id, valor, "D"), PostingLine.Credito(credito.Id, valor, "C")], kind), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        await D.Db.SaveChangesAsync();
    }

    private async Task<Guid> SoporteAsync(string tipo = AdjuntosDeModulo.SoporteDeIndicadorRegulatorio)
    {
        var adjunto = new Attachment
        {
            OwnerEntityType = tipo, OwnerEntityPublicId = Guid.NewGuid(), FileName = "pt.pdf", ContentType = "application/pdf", SizeBytes = 10,
            Sha256Hex = new string('a', 64), StoragePath = "x", Status = EstadoDeAdjunto.Available, CreatedBy = "test",
        };
        D.Db.Attachments.Add(adjunto);
        await D.Db.SaveChangesAsync();
        return adjunto.PublicId;
    }

    private RegisterRegulatoryIndicatorCommandHandler Registro() => new(D.Db, D.User, D.Clock, E.Emisor);

    private async Task<Guid> RegistrarAsync(RegulatoryIndicatorKind tipo, DateOnly corte, decimal valor, Guid? reemplaza = null)
    {
        var r = await Registro().Handle(new RegisterRegulatoryIndicatorCommand(tipo, corte, valor, "Formato de patrimonio técnico", await SoporteAsync(), reemplaza), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    private async Task<IngenIA365ERP.Application.Common.Integration.Accounting.IndicadoresDeLaEntidadDto> IndicadoresAsync(DateOnly? fecha = null)
    {
        var r = await new GetEntityIndicatorsQueryHandler(D.Db, D.Clock).Handle(new GetEntityIndicatorsQuery(fecha ?? Fecha), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    [Fact]
    public async Task El_resultado_del_ejercicio_son_las_clases_4_a_7_sin_el_cierre_y_las_perdidas_la_clase_3_debito()
    {
        var caja = D.Cuenta("11050599");
        var ingreso = D.Cuenta("41500599", AccountNature.Credit);
        var gasto = D.Cuenta("51050599");
        var costo = D.Cuenta("61050599");
        var perdida = D.Cuenta("35100599");
        var excedente = D.Cuenta("35050599", AccountNature.Credit);
        await ContabilizarAsync(new DateOnly(2026, 3, 10), caja, ingreso, 1000m);
        await ContabilizarAsync(new DateOnly(2026, 3, 11), gasto, caja, 300m);
        await ContabilizarAsync(new DateOnly(2026, 3, 12), costo, caja, 100m);
        await ContabilizarAsync(new DateOnly(2026, 3, 13), perdida, caja, 250m);
        await ContabilizarAsync(new DateOnly(2026, 3, 14), caja, excedente, 80m);
        await ContabilizarAsync(new DateOnly(2026, 12, 31), ingreso, excedente, 1000m, DocumentKind.Closing, "CI");

        var i = await IndicadoresAsync(new DateOnly(2026, 12, 31));

        i.Date.Should().Be(new DateOnly(2026, 12, 31));
        i.YearResult.Should().Be(600m, "1.000 de ingreso − 300 de gasto − 100 de costo; el CI del ejercicio no cuenta");
        i.AccumulatedLosses.Should().Be(250m, "sólo la clase 3 de naturaleza débito (C78)");
        i.TechnicalCapital.Should().BeNull();
        i.SolvencyRatio.Should().BeNull();
    }

    [Fact]
    public async Task Patrimonio_y_solvencia_son_el_ultimo_no_reemplazado_con_corte_hasta_la_fecha_sin_juzgar_la_vigencia()
    {
        await RegistrarAsync(RegulatoryIndicatorKind.TechnicalCapital, new DateOnly(2019, 12, 31), 1_000m);
        var febrero = await RegistrarAsync(RegulatoryIndicatorKind.TechnicalCapital, new DateOnly(2026, 2, 28), 2_000m);
        var corregido = await RegistrarAsync(RegulatoryIndicatorKind.TechnicalCapital, new DateOnly(2026, 2, 28), 2_100m, febrero);
        await RegistrarAsync(RegulatoryIndicatorKind.TechnicalCapital, new DateOnly(2026, 4, 30), 9_999m);
        await RegistrarAsync(RegulatoryIndicatorKind.SolvencyRatio, new DateOnly(2019, 12, 31), 0.12m);

        var i = await IndicadoresAsync();

        i.TechnicalCapital!.RegulatoryIndicatorPublicId.Should().Be(corregido, "el de febrero lo reemplazó su corrección; el de abril es posterior a la fecha");
        i.TechnicalCapital.Value.Should().Be(2_100m);
        i.TechnicalCapital.Unit.Should().Be("Pesos");
        i.TechnicalCapital.CutoffDate.Should().Be(new DateOnly(2026, 2, 28));
        i.SolvencyRatio!.CutoffDate.Should().Be(new DateOnly(2019, 12, 31), "Contabilidad devuelve el último con su corte; la vigencia la juzga Cartera (C33)");
        i.SolvencyRatio.Unit.Should().Be("Fraccion");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_responde_NotInitialized()
    {
        var setup = await D.Db.AccountingSetups.SingleAsync();
        setup.IsDeleted = true;
        await D.Db.SaveChangesAsync();

        (await new GetEntityIndicatorsQueryHandler(D.Db, D.Clock).Handle(new GetEntityIndicatorsQuery(Fecha), default)).Error.Code.Should().Be("Accounting.NotInitialized");
    }

    [Fact]
    public async Task Registrar_exige_un_soporte_de_indicador_regulatorio()
    {
        new RegisterRegulatoryIndicatorCommandValidator().Validate(new RegisterRegulatoryIndicatorCommand(RegulatoryIndicatorKind.TechnicalCapital, Fecha, 1m, "x", null))
            .IsValid.Should().BeFalse();
        (await Registro().Handle(new RegisterRegulatoryIndicatorCommand(RegulatoryIndicatorKind.TechnicalCapital, Fecha, 1m, "x", null), default))
            .Error.Code.Should().Be("Accounting.RegulatoryIndicator.SupportRequired");
        (await Registro().Handle(new RegisterRegulatoryIndicatorCommand(RegulatoryIndicatorKind.TechnicalCapital, Fecha, 1m, "x", await SoporteAsync("LendingGuaranteeDocument")), default))
            .Error.Code.Should().Be("Accounting.RegulatoryIndicator.SupportInvalid");
    }

    [Fact]
    public async Task Registrar_deja_el_hecho_con_su_soporte_y_el_evento()
    {
        var reservado = Guid.NewGuid();
        var soporte = await SoporteAsync();

        var r = await Registro().Handle(new RegisterRegulatoryIndicatorCommand(RegulatoryIndicatorKind.SolvencyRatio, new DateOnly(2025, 12, 31), 0.135m,
            "Formato de solvencia de diciembre", soporte, Notes: "Revisado por la revisoría", PublicId: reservado), default);

        r.Value.Should().Be(reservado, "el PublicId que la pantalla reservó para subir el soporte");
        var indicador = await D.Db.RegulatoryIndicators.SingleAsync();
        indicador.SupportAttachmentPublicId.Should().Be(soporte);
        indicador.RegisteredByName.Should().Be(D.User.UserName);
        await E.Auditoria.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.RegulatoryIndicator.Registered"
            && a.EntityPublicId == reservado.ToString()), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Otro_del_mismo_corte_solo_se_registra_reemplazando_y_una_sola_vez()
    {
        var corte = new DateOnly(2025, 12, 31);
        var primero = await RegistrarAsync(RegulatoryIndicatorKind.TechnicalCapital, corte, 1m);

        (await Registro().Handle(new RegisterRegulatoryIndicatorCommand(RegulatoryIndicatorKind.TechnicalCapital, corte, 2m, "x", await SoporteAsync()), default))
            .Error.Code.Should().Be("Accounting.RegulatoryIndicator.AlreadyRegistered");
        var segundo = await RegistrarAsync(RegulatoryIndicatorKind.TechnicalCapital, corte, 2m, primero);
        (await Registro().Handle(new RegisterRegulatoryIndicatorCommand(RegulatoryIndicatorKind.TechnicalCapital, corte, 3m, "x", await SoporteAsync(), primero), default))
            .Error.Code.Should().Be("Accounting.RegulatoryIndicator.SupersedesInvalid", "uno ya reemplazado no se vuelve a reemplazar");
        (await Registro().Handle(new RegisterRegulatoryIndicatorCommand(RegulatoryIndicatorKind.TechnicalCapital, corte.AddMonths(-1), 3m, "x", await SoporteAsync(), segundo), default))
            .Error.Code.Should().Be("Accounting.RegulatoryIndicator.SupersedesInvalid", "sólo se corrige uno del mismo corte");

        var lista = (await new ListRegulatoryIndicatorsQueryHandler(D.Db).Handle(new ListRegulatoryIndicatorsQuery(), default)).Value;
        lista.Single(x => x.RegulatoryIndicatorPublicId == primero).SupersededByPublicId.Should().Be(segundo);
        lista.Single(x => x.RegulatoryIndicatorPublicId == segundo).SupersedesPublicId.Should().Be(primero);
    }
}
