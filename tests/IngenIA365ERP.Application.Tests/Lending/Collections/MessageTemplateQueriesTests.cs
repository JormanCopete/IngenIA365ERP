using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Feature 013, T0272 (US17; T76; contracts/api.md §14.1; adaptadores-y-archivos.md §4.7): las plantillas de mensaje de Cartera se listan
/// por propósito, canal y fecha (<see cref="ListMessageTemplatesQuery"/>), tienen historial por código
/// (<see cref="GetMessageTemplateHistoryQuery"/>, <c>Lending.MessageTemplate.NotFound</c>), la versión nueva rechaza una variable fuera de la
/// lista blanca (<c>.VariableNotAllowed</c> con <c>data.allowed[]</c>), avisa el SMS largo (<c>.SmsTooLong</c>) y no se cruza con otra
/// (<c>.Overlaps</c>); la vista previa (<see cref="RenderMessageTemplatePreviewQuery"/>) no lleva clave ni escribe.
/// </summary>
public sealed class MessageTemplateQueriesTests : IDisposable
{
    private readonly MensajesDePrueba _e = new();

    public void Dispose() => _e.Dispose();

    private CreateMessageTemplateVersionCommandHandler Versionar()
    {
        _e.G.ActuarComo(_e.G.Usuario(null, "jefe@coop.test"));
        return new CreateMessageTemplateVersionCommandHandler(_e.Db, _e.G.Actor, _e.G.Reloj);
    }

    [Fact]
    public async Task Lista_las_vigentes_por_proposito_canal_y_fecha()
    {
        _e.Plantilla("ABONOCAP", DebtorMessagePurpose.Informative, MessageChannel.Email, "Nuevo", "Texto nuevo {{NombreDelTitular}}", version: 2,
            desde: new DateOnly(2027, 1, 1));
        var handler = new ListMessageTemplatesQueryHandler(_e.Db, _e.G.Reloj);

        var cobranza = await handler.Handle(new ListMessageTemplatesQuery(DebtorMessagePurpose.Collection), default);
        var correos = await handler.Handle(new ListMessageTemplatesQuery(Channel: MessageChannel.Email), default);
        var enEnero = await handler.Handle(new ListMessageTemplatesQuery(DebtorMessagePurpose.Informative, MessageChannel.Email, new DateOnly(2027, 1, 15)), default);

        cobranza.Value.Should().HaveCount(2).And.OnlyContain(t => t.Code == "MORAADMIN");
        correos.Value.Select(t => t.Code).Should().BeEquivalentTo(["ABONOCAP", "MORAADMIN"]);
        correos.Value.Single(t => t.Code == "ABONOCAP").Version.Should().Be(1, "hoy rige la 1");
        enEnero.Value.Should().ContainSingle(t => t.Version == 2);
        cobranza.Value.First().Variables.Should().Contain("DiasDeMora");
    }

    [Fact]
    public async Task El_historial_trae_todas_las_versiones_y_un_codigo_que_no_existe_es_NotFound()
    {
        var r = await Versionar().Handle(new CreateMessageTemplateVersionCommand("MORAADMIN", DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            "Nuevo {{NumeroDelCredito}}", null, null, new DateOnly(2026, 12, 15), "Revisado por el abogado"), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var handler = new GetMessageTemplateHistoryQueryHandler(_e.Db);

        var historial = await handler.Handle(new GetMessageTemplateHistoryQuery("moraadmin", MessageChannel.Sms), default);
        var noExiste = await handler.Handle(new GetMessageTemplateHistoryQuery("NOEXISTE"), default);

        historial.Value.Select(v => v.Version).Should().Equal(2, 1);
        historial.Value.Single(v => v.Version == 1).ValidTo.Should().Be(new DateOnly(2026, 12, 14));
        noExiste.Error.Code.Should().Be("Lending.MessageTemplate.NotFound");
    }

    [Fact]
    public async Task Una_variable_fuera_de_la_lista_blanca_se_rechaza_con_la_lista()
    {
        var r = await Versionar().Handle(new CreateMessageTemplateVersionCommand("MORAADMIN", DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            "Hola {{CedulaDelCodeudor}}", null, null, new DateOnly(2026, 12, 15), "Prueba"), default);

        r.Error.Code.Should().Be("Lending.MessageTemplate.VariableNotAllowed");
        var datos = GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("variable").GetString().Should().Be("CedulaDelCodeudor");
        datos.GetProperty("allowed").EnumerateArray().Select(a => a.GetString()).Should().Contain("ValorVencido");
    }

    [Fact]
    public async Task El_SMS_largo_se_guarda_con_el_aviso_SmsTooLong()
    {
        var r = await Versionar().Handle(new CreateMessageTemplateVersionCommand("MORAADMIN", DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            new string('a', 170) + " {{NumeroDelCredito}}", null, null, new DateOnly(2026, 12, 15), "Prueba"), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Warnings.Should().ContainSingle(w => w.Codigo == "Lending.MessageTemplate.SmsTooLong");
    }

    [Fact]
    public async Task Dos_vigencias_que_se_cruzan_responden_Overlaps()
    {
        var r = await Versionar().Handle(new CreateMessageTemplateVersionCommand("MORAADMIN", DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            "Texto {{NumeroDelCredito}}", null, null, new DateOnly(2019, 6, 1), "Prueba"), default);

        r.Error.Code.Should().Be("Lending.MessageTemplate.Overlaps");
    }

    [Fact]
    public async Task La_vista_previa_no_lleva_clave_ni_escribe_y_mide_el_SMS()
    {
        var handler = new RenderMessageTemplatePreviewQueryHandler(_e.Db, _e.G.Alcance, _e.G.Reloj);
        var cambios = _e.Db.ChangeTracker.Entries().Count();

        var deCodigo = await handler.Handle(new RenderMessageTemplatePreviewQuery(MessageChannel.Sms, Code: "MORAADMIN",
            SampleLoanPublicId: _e.Credito.PublicId), default);
        var libre = await handler.Handle(new RenderMessageTemplatePreviewQuery(MessageChannel.Sms, Body: new string('b', 200),
            Purpose: DebtorMessagePurpose.Informative), default);
        var invalida = await handler.Handle(new RenderMessageTemplatePreviewQuery(MessageChannel.Sms, Body: "{{Clave}}",
            Purpose: DebtorMessagePurpose.Informative), default);

        typeof(IOperacionIdempotente).IsAssignableFrom(typeof(RenderMessageTemplatePreviewQuery)).Should().BeFalse("la vista previa no lleva clave");
        deCodigo.Value.Body.Should().Contain("CR-0001").And.Contain("Ana Deudora");
        libre.Value.Length.Should().Be(200);
        libre.Value.Warnings.Should().Contain(w => w.Codigo == "Lending.MessageTemplate.SmsTooLong");
        invalida.Error.Code.Should().Be("Lending.MessageTemplate.VariableNotAllowed");
        _e.Db.ChangeTracker.Entries().Count().Should().Be(cambios);
    }

    [Fact]
    public async Task La_vista_previa_ignora_el_credito_de_ejemplo_fuera_del_alcance()
    {
        // Bloque F (T0259): el crédito de ejemplo es de Norte; quien sólo tiene Sur ve los valores de muestra, no el número ni el titular.
        _e.G.AlcanceActual = AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { MensajesDePrueba.Sur }, TodasLasLineas = true };
        var handler = new RenderMessageTemplatePreviewQueryHandler(_e.Db, _e.G.Alcance, _e.G.Reloj);

        var vista = await handler.Handle(new RenderMessageTemplatePreviewQuery(MessageChannel.Sms, Code: "MORAADMIN",
            SampleLoanPublicId: _e.Credito.PublicId), default);

        vista.Value.Body.Should().NotContain("CR-0001").And.NotContain("Ana Deudora").And.Contain("[NumeroDelCredito]");
    }
}
