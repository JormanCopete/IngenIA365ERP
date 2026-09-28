using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Channels;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales;

/// <summary>Los escenarios que todo adaptador sabe provocar para la batería de conformidad. (nuevo)</summary>
public enum CasoDeConformidad
{
    /// <summary>La DIAN lo valida sin mensajes.</summary>
    Validado = 1,

    /// <summary>La DIAN lo valida con notificaciones.</summary>
    ConNotificaciones = 2,

    /// <summary>La DIAN (o el canal) lo rechaza por una regla de negocio.</summary>
    Rechazado = 3,

    /// <summary>La petición no salió (DNS, conexión rehusada, credencial inválida antes de enviar).</summary>
    SinSalir = 4,

    /// <summary>La petición pudo haber llegado (tiempo agotado después de enviar, 5xx, corte con el cuerpo enviado).</summary>
    Ambiguo = 5,

    /// <summary>El canal informa que la DIAN no está disponible y entrega el documento firmado.</summary>
    DianNoDisponible = 6,
}

/// <summary>
/// La batería reutilizable de conformidad del canal (feature 012, I4, T683; contracts/dian.md §3.3): las ocho reglas que cumple todo
/// adaptador de <see cref="ICanalDeEmisionElectronica"/>. <c>CanalSimulado</c> la hereda (<see cref="CanalSimuladoConformidadTests"/>) y todo
/// adaptador futuro —el del proveedor tecnológico (T732) y <c>CanalServicioCentral</c> (T733)— la hereda con su propio escenario
/// (sandbox o servidor falso). Una clase derivada sólo dice cómo provocar cada <see cref="CasoDeConformidad"/>. (nuevo)
/// </summary>
public abstract class ConformidadDelCanal
{
    /// <summary>La cooperativa de la batería.</summary>
    protected static readonly Guid Cooperativa = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");

    /// <summary>El adaptador bajo prueba (xUnit crea una instancia de la clase por prueba: uno nuevo cada vez).</summary>
    protected abstract ICanalDeEmisionElectronica Canal { get; }

    /// <summary>Las opciones técnicas del canal (regla 8).</summary>
    protected abstract OpcionesDeCanal Opciones { get; }

    /// <summary>Un documento que provoca <paramref name="caso"/> con el consecutivo dado.</summary>
    protected abstract DocumentoElectronicoCanonico Documento(CasoDeConformidad caso, long consecutivo);

    /// <summary>Cuántas peticiones llegaron al transporte para el número de <paramref name="documento"/>; nulo si el adaptador no lo sabe contar.</summary>
    protected virtual int? PeticionesAlTransporte(DocumentoElectronicoCanonico documento) => null;

    /// <summary>El contexto de la cooperativa de la batería, con la clave de idempotencia de la versión.</summary>
    protected virtual ContextoDeCanal Contexto(DocumentoElectronicoCanonico documento, int version = 1) => new(
        Cooperativa, "900123456", "7", EmissionMode.TechnologyProvider, documento.Environment, null, null, null,
        ContextoDeCanal.ClaveDeIdempotencia(Cooperativa, documento.Environment, documento.Number.Prefix, documento.Number.Consecutive, version),
        null);

    protected static ReferenciaDeEnvio Referencia(DocumentoElectronicoCanonico documento, string? codigoUnico = null) =>
        new(documento.Number.Prefix, documento.Number.Consecutive, documento.Environment, codigoUnico);

    private static readonly CasoDeConformidad[] Todos = Enum.GetValues<CasoDeConformidad>();

    // ------------------------------------------------------------------------------------------------ regla 1 --

    [Fact]
    public async Task Regla1_nunca_lanza_por_un_rechazo_de_negocio()
    {
        var documento = Documento(CasoDeConformidad.Rechazado, 990000101);

        var resultado = await Canal.EmitirAsync(documento, Contexto(documento), default);

        Assert.Contains(resultado.Outcome, new[] { ChannelOutcome.Rejected, ChannelOutcome.InvalidData });
        Assert.Contains(resultado.Mensajes, m => m.Tipo == TipoDeMensajeDelCanal.Rechazo && !string.IsNullOrWhiteSpace(m.Regla));
    }

    // ------------------------------------------------------------------------------------------------ regla 2 --

    [Fact]
    public async Task Regla2_si_la_peticion_no_salio_es_ChannelUnavailable_sin_excepcion()
    {
        var documento = Documento(CasoDeConformidad.SinSalir, 990000102);

        var resultado = await Canal.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(ChannelOutcome.ChannelUnavailable, resultado.Outcome);
        Assert.False(resultado.TieneCodigoUnico);
    }

    [Fact]
    public async Task Regla2_si_la_peticion_pudo_llegar_es_InProcess_sin_excepcion()
    {
        var documento = Documento(CasoDeConformidad.Ambiguo, 990000103);

        var resultado = await Canal.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(ChannelOutcome.InProcess, resultado.Outcome);
    }

    // ------------------------------------------------------------------------------------------------ regla 3 --

    [Fact]
    public async Task Regla3_ya_existe_se_traduce_a_una_consulta()
    {
        var documento = Documento(CasoDeConformidad.Validado, 990000104);
        var primero = await Canal.EmitirAsync(documento, Contexto(documento), default);

        var repetido = await Canal.EmitirAsync(documento, Contexto(documento), default);
        var consulta = await Canal.ConsultarEstadoAsync(Referencia(documento, primero.UniqueCode), Contexto(documento), default);

        Assert.Equal(ChannelOutcome.Validated, primero.Outcome);
        Assert.Equal(consulta.Outcome, repetido.Outcome);
        Assert.Equal(primero.UniqueCode, repetido.UniqueCode);
        Assert.Equal(primero.UniqueCode, consulta.UniqueCode);
    }

    [Fact]
    public async Task Regla3_la_consulta_de_un_numero_que_nunca_llego_es_NotFound()
    {
        var documento = Documento(CasoDeConformidad.Validado, 990000199);

        var consulta = await Canal.ConsultarEstadoAsync(Referencia(documento), Contexto(documento), default);

        Assert.Equal(ChannelOutcome.NotFound, consulta.Outcome);
    }

    // ------------------------------------------------------------------------------------------------ regla 4 --

    [Fact]
    public async Task Regla4_todo_resultado_es_un_ChannelOutcome_y_conserva_el_codigo_crudo_en_su_medida()
    {
        long consecutivo = 990000110;
        foreach (var caso in Todos)
        {
            var documento = Documento(caso, consecutivo++);
            var resultado = await Canal.EmitirAsync(documento, Contexto(documento), default);

            Assert.True(Enum.IsDefined(resultado.Outcome), $"{caso}: {resultado.Outcome} no es un ChannelOutcome");
            Assert.True((resultado.ProviderCode?.Length ?? 0) <= 20, $"{caso}: ProviderCode excede 20");
            Assert.True((resultado.DianStatusCode?.Length ?? 0) <= 10, $"{caso}: DianStatusCode excede 10");
            Assert.True((resultado.ExternalReference?.Length ?? 0) <= 100, $"{caso}: ExternalReference excede 100");
            Assert.True(resultado.DurationMs >= 0);
        }
    }

    // ------------------------------------------------------------------------------------------------ regla 5 --

    [Fact]
    public async Task Regla5_Validated_exige_codigo_unico_y_respuesta_de_validacion()
    {
        long consecutivo = 990000120;
        foreach (var caso in Todos)
        {
            var documento = Documento(caso, consecutivo++);
            var emitido = await Canal.EmitirAsync(documento, Contexto(documento), default);
            var consultado = await Canal.ConsultarEstadoAsync(Referencia(documento, emitido.UniqueCode), Contexto(documento), default);

            foreach (var r in new[] { emitido, consultado })
            {
                if (r.Outcome is not (ChannelOutcome.Validated or ChannelOutcome.ValidatedWithNotices)) continue;
                Assert.True(r.TieneCodigoUnico, $"{caso}: validado sin código único");
                Assert.True(r.TieneRespuestaDeValidacion, $"{caso}: validado sin respuesta de validación");
                Assert.Contains(r.UniqueCodeKind, new[] { "CUFE", "CUDE", "CUDS" });
            }
        }
    }

    [Fact]
    public async Task Regla5_DianUnavailable_trae_el_documento_firmado_y_su_codigo()
    {
        var documento = Documento(CasoDeConformidad.DianNoDisponible, 990000105);

        var resultado = await Canal.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(ChannelOutcome.DianUnavailable, resultado.Outcome);
        Assert.True(resultado.TieneCodigoUnico);
        Assert.Contains(resultado.Artefactos, a => a.Tipo == TipoDeArtefacto.XmlFirmado && a.Bytes.Length > 0);
        Assert.False(string.IsNullOrWhiteSpace(resultado.DianDocumentTypeCode));
    }

    // ------------------------------------------------------------------------------------------------ regla 6 --

    [Fact]
    public async Task Regla6_las_notificaciones_solas_no_rechazan()
    {
        var documento = Documento(CasoDeConformidad.ConNotificaciones, 990000106);

        var resultado = await Canal.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(ChannelOutcome.ValidatedWithNotices, resultado.Outcome);
        Assert.NotEmpty(resultado.Mensajes);
        Assert.All(resultado.Mensajes, m => Assert.Equal(TipoDeMensajeDelCanal.Notificacion, m.Tipo));
    }

    [Fact]
    public async Task Regla6_un_validado_sin_mensajes_es_Validated()
    {
        var documento = Documento(CasoDeConformidad.Validado, 990000107);

        var resultado = await Canal.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(ChannelOutcome.Validated, resultado.Outcome);
        Assert.DoesNotContain(resultado.Mensajes, m => m.Tipo == TipoDeMensajeDelCanal.Rechazo);
    }

    // ------------------------------------------------------------------------------------------------ regla 7 --

    [Fact]
    public async Task Regla7_EmitirAsync_no_reintenta_por_su_cuenta()
    {
        foreach (var (caso, consecutivo) in new[] { (CasoDeConformidad.SinSalir, 990000130L), (CasoDeConformidad.Ambiguo, 990000131L) })
        {
            var documento = Documento(caso, consecutivo);
            await Canal.EmitirAsync(documento, Contexto(documento), default);

            var peticiones = PeticionesAlTransporte(documento);
            if (peticiones is { } n) Assert.True(n <= 1, $"{caso}: {n} peticiones en una sola emisión");
        }
    }

    // ------------------------------------------------------------------------------------------------ regla 8 --

    [Fact]
    public void Regla8_los_tiempos_del_transporte_son_configurables_y_la_conexion_es_corta()
    {
        Assert.True(Opciones.ConnectTimeoutSeconds > 0);
        Assert.True(Opciones.TotalTimeoutSeconds >= Opciones.ConnectTimeoutSeconds);
    }

    // ------------------------------------------------------------------------------------------------ generales --

    [Fact]
    public void El_canal_acepta_el_numero_del_ERP_y_tiene_codigo_estable()
    {
        Assert.False(string.IsNullOrWhiteSpace(Canal.ChannelCode));
        Assert.Equal(Canal.ChannelCode.Trim().ToUpperInvariant(), Canal.ChannelCode);
        Assert.True(Canal.Capacidades.AceptaNumeroDelErp);
    }

    [Fact]
    public async Task Probar_no_lanza()
    {
        var documento = Documento(CasoDeConformidad.Validado, 990000140);

        var resultado = await Canal.ProbarAsync(Contexto(documento), default);

        Assert.True(Enum.IsDefined(resultado.Outcome));
    }

    /// <summary>Un documento mínimo de factura con la contraparte dada, para que las derivadas no repitan la forma.</summary>
    protected static DocumentoElectronicoCanonico Factura(string taxIdContraparte, long consecutivo, string prefijo = "SETP",
        ElectronicDocumentKind tipo = ElectronicDocumentKind.Invoice, string tipoDian = "01", DianEnvironment ambiente = DianEnvironment.Testing) => new()
    {
        Kind = tipo,
        DianDocumentTypeCode = tipoDian,
        OperationTypeCode = "10",
        Environment = ambiente,
        Number = new NumeroCanonico(prefijo, consecutivo, prefijo + consecutivo.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        IssuedAt = new DateTimeOffset(2026, 12, 5, 10, 0, 0, TimeSpan.FromHours(-5)),
        Currency = "COP",
        PaymentForm = "Cash",
        Issuer = new ParteCanonica { TaxId = "900123456", CheckDigit = "7", IdTypeCode = "31", PersonTypeCode = "1", Name = "Cooperativa de prueba" },
        Counterparty = new ParteCanonica { Role = "Buyer", TaxId = taxIdContraparte, IdTypeCode = "13", PersonTypeCode = "2", Name = "Comprador" },
        Totals = new TotalesCanonicos { LineExtension = 4200m, TaxExclusive = 4200m, Taxes = 798m, TaxInclusive = 4998m, Payable = 4998m, AmountDue = 4998m },
    };
}
