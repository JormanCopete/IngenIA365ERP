using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// <c>RenderizadorDePlantillasDeMensaje</c> y <c>CreateMessageTemplateVersionCommand</c> (feature 013, T0032; T76;
/// contracts/adaptadores-y-archivos.md §4.7): sólo variables de la lista blanca del propósito (las comunes más las del propósito); una
/// variable fuera de la lista es error al guardar la plantilla; HTML escapado en el correo; aviso del largo del SMS (160 GSM-7, 70 UCS-2) y
/// número de segmentos; WhatsApp sin <c>ProviderTemplateId</c> no se usa; cambiar una plantilla crea <c>Version + 1</c> y cierra la
/// anterior la víspera; dos versiones del mismo (<c>Code</c>, <c>Channel</c>) no se cruzan. También la semilla de las nueve plantillas
/// (T0085). (nuevo)
/// </summary>
public sealed class RenderizadorDePlantillasDeMensajeTests
{
    private static readonly IReadOnlyDictionary<string, string?> Valores = new Dictionary<string, string?>
    {
        ["NombreDelTitular"] = "Ana <b>Pérez</b> & Cía",
        ["NombreDeLaCooperativa"] = "Cooperativa de ensayo",
        ["NumeroDelCredito"] = "CR-0001",
        ["FechaDeCorte"] = "2026-12-05",
        ["ValorVencido"] = "$ 150.000",
        ["DiasDeMora"] = "12",
    };

    // ------------------------------------------------------------------------------------------------------------ lista blanca --

    [Fact]
    public void La_lista_blanca_es_la_comun_mas_la_del_proposito()
    {
        var cobranza = RenderizadorDePlantillasDeMensaje.VariablesPermitidas(DebtorMessagePurpose.Collection);
        var documentos = RenderizadorDePlantillasDeMensaje.VariablesPermitidas(DebtorMessagePurpose.DocumentDelivery);

        Assert.All(RenderizadorDePlantillasDeMensaje.VariablesComunes, v => Assert.Contains(v, cobranza));
        Assert.Contains("DiasDeMora", cobranza);
        Assert.DoesNotContain("CodigoDeVerificacion", cobranza);
        Assert.Contains("CodigoDeVerificacion", documentos);
        Assert.DoesNotContain("DiasDeMora", documentos);
    }

    [Theory]
    [InlineData("Hola {{DiasDeMora}}")]                        // de otro propósito
    [InlineData("Hola {{ReferenciaPersonal}}")]                // no existe
    [InlineData("Hola {{NombreDelTitular.ToUpper()}}")]        // expresión
    [InlineData("Hola {{#if ValorVencido}}x{{/if}}")]          // bloque
    public void Una_variable_fuera_de_la_lista_es_error_al_guardar(string cuerpo)
    {
        var revision = RenderizadorDePlantillasDeMensaje.Revisar(DebtorMessagePurpose.DocumentDelivery, MessageChannel.Email, "Asunto", cuerpo, null);

        Assert.False(revision.EsValida);
        var error = Assert.IsType<ErrorConDatos>(revision.Errores[0]);
        Assert.Equal("Lending.MessageTemplate.VariableNotAllowed", error.Code);
    }

    [Fact]
    public void Una_variable_fuera_de_la_lista_en_el_asunto_tambien_es_error()
    {
        var revision = RenderizadorDePlantillasDeMensaje.Revisar(DebtorMessagePurpose.Informative, MessageChannel.Email,
            "Mora de {{DiasDeMora}} días", "Hola {{NombreDelTitular}}", null);

        Assert.False(revision.EsValida);
    }

    [Fact]
    public void Las_variables_permitidas_se_sustituyen_y_admiten_espacios_dentro_del_marcador()
    {
        var r = RenderizadorDePlantillasDeMensaje.Renderizar(DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            "{{ NombreDeLaCooperativa }}: {{NumeroDelCredito}} con {{DiasDeMora}} dias a {{FechaDeCorte}}", null, Valores);

        Assert.True(r.IsSuccess);
        Assert.Equal("Cooperativa de ensayo: CR-0001 con 12 dias a 2026-12-05", r.Value.Cuerpo);
    }

    // ---------------------------------------------------------------------------------------------------------------- correo --

    [Fact]
    public void En_el_correo_se_escapa_el_HTML_de_los_valores_no_el_de_la_plantilla()
    {
        var r = RenderizadorDePlantillasDeMensaje.Renderizar(DebtorMessagePurpose.Collection, MessageChannel.Email,
            "Crédito {{NumeroDelCredito}} de {{NombreDelTitular}}", "<p>Señor(a) {{NombreDelTitular}}</p>", null, Valores);

        Assert.True(r.IsSuccess);
        Assert.Equal("<p>Señor(a) Ana &lt;b&gt;P&#233;rez&lt;/b&gt; &amp; C&#237;a</p>", r.Value.Cuerpo);
        // El asunto es texto plano: no se escapa.
        Assert.Equal("Crédito CR-0001 de Ana <b>Pérez</b> & Cía", r.Value.Asunto);
    }

    [Fact]
    public void En_el_SMS_no_se_escapa()
    {
        var r = RenderizadorDePlantillasDeMensaje.Renderizar(DebtorMessagePurpose.Collection, MessageChannel.Sms, null, "{{NombreDelTitular}}",
            null, Valores);

        Assert.Equal("Ana <b>Pérez</b> & Cía", r.Value.Cuerpo);
    }

    [Fact]
    public void Una_variable_permitida_sin_valor_queda_vacia_con_aviso()
    {
        var r = RenderizadorDePlantillasDeMensaje.Renderizar(DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            "Pague {{ValorParaPonerseAlDia}}.", null, Valores);

        Assert.True(r.IsSuccess);
        Assert.Equal("Pague .", r.Value.Cuerpo);
        Assert.Contains(r.Value.Avisos, a => a.Codigo == "Lending.MessageTemplate.VariableWithoutValue");
    }

    // ------------------------------------------------------------------------------------------------------------------- SMS --

    [Fact]
    public void Un_SMS_en_GSM7_cabe_en_160_y_desde_161_avisa_y_cuenta_segmentos_de_153()
    {
        var corto = RenderizadorDePlantillasDeMensaje.MedirSms(new string('a', 160));
        var largo = RenderizadorDePlantillasDeMensaje.MedirSms(new string('a', 161));
        var tres = RenderizadorDePlantillasDeMensaje.MedirSms(new string('a', 307));

        Assert.Equal(new LargoDeSms(160, true, 160, 1), corto);
        Assert.Equal(new LargoDeSms(161, true, 160, 2), largo);
        Assert.Equal(3, tres.Segmentos);
    }

    [Fact]
    public void Una_tilde_fuera_del_alfabeto_basico_pasa_el_SMS_a_UCS2_de_70()
    {
        var conTilde = RenderizadorDePlantillasDeMensaje.MedirSms("Crédito" + new string('a', 63) + "ó"); // 71 caracteres
        var setenta = RenderizadorDePlantillasDeMensaje.MedirSms(new string('a', 69) + "á");

        Assert.False(conTilde.Gsm7);
        Assert.Equal(new LargoDeSms(71, false, 70, 2), conTilde);
        Assert.Equal(new LargoDeSms(70, false, 70, 1), setenta);
    }

    [Fact]
    public void Los_caracteres_de_la_extension_GSM7_valen_dos()
    {
        Assert.Equal(new LargoDeSms(5, true, 160, 1), RenderizadorDePlantillasDeMensaje.MedirSms("€{é"));
        Assert.Equal(2, RenderizadorDePlantillasDeMensaje.MedirSms("€").Largo);
    }

    [Fact]
    public void La_revision_de_un_SMS_largo_es_un_aviso_con_largo_limite_y_segmentos_no_un_error()
    {
        var revision = RenderizadorDePlantillasDeMensaje.Revisar(DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            new string('a', 200) + " {{NumeroDelCredito}}", null);

        Assert.True(revision.EsValida);
        var aviso = Assert.Single(revision.Avisos);
        Assert.Equal("Lending.MessageTemplate.SmsTooLong", aviso.Codigo);
        Assert.Equal(2, revision.Sms!.Segmentos);
    }

    // -------------------------------------------------------------------------------------------------------------- WhatsApp --

    [Fact]
    public void WhatsApp_sin_ProviderTemplateId_no_se_usa()
    {
        var revision = RenderizadorDePlantillasDeMensaje.Revisar(DebtorMessagePurpose.Collection, MessageChannel.WhatsApp, null, "Hola", null);
        var sin = RenderizadorDePlantillasDeMensaje.Renderizar(DebtorMessagePurpose.Collection, MessageChannel.WhatsApp, null, "Hola", null, Valores);
        var con = RenderizadorDePlantillasDeMensaje.Renderizar(DebtorMessagePurpose.Collection, MessageChannel.WhatsApp, null, "Hola", "cobranza_v1",
            Valores);

        Assert.True(revision.EsValida);
        Assert.Contains(revision.Avisos, a => a.Codigo == "Lending.MessageTemplate.ProviderTemplateMissing");
        Assert.True(sin.IsFailure);
        Assert.Equal("Lending.MessageTemplate.ProviderTemplateMissing", sin.Error.Code);
        Assert.True(con.IsSuccess);
    }

    // ------------------------------------------------------------------------------------------------------------- versiones --

    private static readonly DateOnly Enero = new(2026, 1, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private CreateMessageTemplateVersionCommandHandler Handler()
    {
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 3, Guid.NewGuid(), Guid.NewGuid(),
            "Analista de cartera", "analista@coop.test", ExecutionChannel.Web, "POST /api/lending/message-templates", "10.0.0.2", null));
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(new DateTime(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc));
        return new CreateMessageTemplateVersionCommandHandler(_db, actor, reloj);
    }

    private static CreateMessageTemplateVersionCommand Version(DateOnly desde, string cuerpo = "Hola {{NombreDelTitular}}",
        MessageChannel canal = MessageChannel.Sms, DebtorMessagePurpose proposito = DebtorMessagePurpose.Collection) =>
        new("moraadmin", proposito, canal, canal == MessageChannel.Email ? "Asunto" : null, cuerpo, null, null, desde, "Texto revisado por el abogado");

    [Fact]
    public async Task La_primera_version_es_la_1_y_guarda_la_lista_blanca_del_proposito()
    {
        var r = await Handler().Handle(Version(Enero), default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal("MORAADMIN", r.Value.Code);
        Assert.Equal(1, r.Value.Version);
        var fila = await _db.NotificationTemplates.SingleAsync();
        Assert.Equal("Lending", fila.Module);
        Assert.Equal("Collection", fila.Purpose);
        Assert.Equal("Sms", fila.Channel);
        Assert.Contains("\"DiasDeMora\"", fila.Variables, StringComparison.Ordinal);
        Assert.False(fila.IsSeeded);
    }

    [Fact]
    public async Task Cambiar_una_plantilla_crea_la_version_siguiente_y_cierra_la_anterior_la_vispera()
    {
        await Handler().Handle(Version(Enero), default);

        var r = await Handler().Handle(Version(new DateOnly(2026, 7, 1), "Nuevo texto {{NumeroDelCredito}}"), default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(2, r.Value.Version);
        var filas = await _db.NotificationTemplates.OrderBy(t => t.Version).ToListAsync();
        Assert.Equal(2, filas.Count);
        Assert.Equal(new DateOnly(2026, 6, 30), filas[0].ValidTo);
        Assert.Equal("Hola {{NombreDelTitular}}", filas[0].BodyTemplate); // lo que ya salió nunca cambia
        Assert.Null(filas[1].ValidTo);
        Assert.NotEqual(filas[0].PublicId, filas[1].PublicId);
    }

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2025, 12, 1)]
    public async Task Dos_versiones_del_mismo_codigo_y_canal_no_se_cruzan(int anio, int mes, int dia)
    {
        await Handler().Handle(Version(Enero), default);

        var r = await Handler().Handle(Version(new DateOnly(anio, mes, dia)), default);

        Assert.True(r.IsFailure);
        Assert.Equal("Lending.MessageTemplate.Overlaps", r.Error.Code);
        Assert.Single(await _db.NotificationTemplates.ToListAsync());
    }

    [Fact]
    public async Task Otro_canal_del_mismo_codigo_es_otra_plantilla()
    {
        await Handler().Handle(Version(Enero), default);

        var r = await Handler().Handle(Version(Enero, canal: MessageChannel.Email), default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(1, r.Value.Version);
    }

    [Fact]
    public async Task Una_variable_fuera_de_la_lista_no_se_guarda()
    {
        var r = await Handler().Handle(Version(Enero, "Su referencia {{TelefonoDeLaReferencia}}"), default);

        Assert.True(r.IsFailure);
        Assert.Equal("Lending.MessageTemplate.VariableNotAllowed", r.Error.Code);
        Assert.Empty(await _db.NotificationTemplates.ToListAsync());
    }

    [Fact]
    public async Task Una_variable_declarada_fuera_de_la_lista_tampoco()
    {
        var comando = Version(Enero) with { Variables = ["NombreDelTitular", "CodigoDeVerificacion"] };

        var r = await Handler().Handle(comando, default);

        Assert.Equal("Lending.MessageTemplate.VariableNotAllowed", r.Error.Code);
    }

    [Fact]
    public async Task Una_version_nueva_no_cambia_el_proposito()
    {
        await Handler().Handle(Version(Enero), default);

        var r = await Handler().Handle(Version(new DateOnly(2026, 7, 1), proposito: DebtorMessagePurpose.Informative), default);

        Assert.Equal("Lending.MessageTemplate.PurposeMismatch", r.Error.Code);
    }

    [Fact]
    public async Task El_SMS_largo_se_guarda_con_el_aviso()
    {
        var r = await Handler().Handle(Version(Enero, new string('a', 170)), default);

        Assert.True(r.IsSuccess);
        Assert.Contains(r.Value.Warnings, a => a.Codigo == "Lending.MessageTemplate.SmsTooLong");
    }

    // ---------------------------------------------------------------------------------------------------------------- semilla --

    [Fact]
    public void La_semilla_trae_las_doce_plantillas_por_correo_SMS_y_WhatsApp_la_simulacion_por_correo_y_todas_son_validas()
    {
        var plantillas = LendingMessageTemplatesSeeder.Leer();

        Assert.Equal(
            ["ABONOCAP", "AVISOPREV", "CAMBIOTASA", "CONTRATINC", "ENVIODOC", "GASTOCOBR", "INACTIVA", "MAYOREDAD", "MORAADMIN", "NOAPLICPAG", "NORECLAMA", "PASOACAJA",
                "PREINACTIV", "PREJURID", "RECORDPREV", "SIMULACION"],
            plantillas.Select(p => p.Code).Distinct().Order(StringComparer.Ordinal));
        // Doce por correo, SMS y WhatsApp (la feature 014 suma CAMBIOTASA, T067, MAYOREDAD, T076, y CONTRATINC, T182; US14 suma PREINACTIV, INACTIVA y
        // NORECLAMA, T208, T209), más SIMULACION sólo por correo (US2, T0390).
        Assert.Equal(46, plantillas.Count);
        Assert.All(plantillas, p => Assert.True(p.Code.Length <= 10));
        Assert.All(plantillas.Where(p => p.Channel == MessageChannel.Email), p => Assert.False(string.IsNullOrWhiteSpace(p.Subject)));
        Assert.All(plantillas.Where(p => p.Channel == MessageChannel.Sms),
            p => Assert.True(RenderizadorDePlantillasDeMensaje.MedirSms(p.Body).Gsm7, $"{p.Code}: el SMS sembrado debe quedarse en GSM-7."));
        Assert.All(plantillas, p => Assert.DoesNotContain("SOLIDO", p.Body, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task La_semilla_es_idempotente_y_marca_las_filas_pendientes_de_validar()
    {
        var primera = await LendingMessageTemplatesSeeder.AplicarAsync(_db, default);
        var segunda = await LendingMessageTemplatesSeeder.AplicarAsync(_db, default);

        Assert.Equal(46, primera);
        Assert.Equal(0, segunda);
        var filas = await _db.NotificationTemplates.ToListAsync();
        Assert.All(filas, f =>
        {
            Assert.True(f.IsSeeded);
            Assert.True(f.ReviewPending);
            Assert.Equal("Lending", f.Module);
            Assert.Equal(1, f.Version);
        });
        Assert.Equal("PriorNotice", filas.First(f => f.Code == "AVISOPREV").Purpose);
    }

    [Fact]
    public async Task Una_plantilla_sembrada_que_la_cooperativa_cambio_no_vuelve()
    {
        await LendingMessageTemplatesSeeder.AplicarAsync(_db, default);
        var r = await Handler().Handle(new CreateMessageTemplateVersionCommand("MORAADMIN", DebtorMessagePurpose.Collection, MessageChannel.Sms, null,
            "Texto propio {{NumeroDelCredito}}", null, null, new DateOnly(2026, 7, 1), "Texto de la cooperativa"), default);
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);

        Assert.Equal(0, await LendingMessageTemplatesSeeder.AplicarAsync(_db, default));
        Assert.Equal(2, await _db.NotificationTemplates.CountAsync(t => t.Code == "MORAADMIN" && t.Channel == "Sms"));
    }
}
