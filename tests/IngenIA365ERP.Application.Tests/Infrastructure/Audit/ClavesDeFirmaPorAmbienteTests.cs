using FluentAssertions;
using IngenIA365ERP.Audit;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Services;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Domain.Entities.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.Infrastructure.Audit;

/// <summary>
/// Feature 012, T986: un ambiente con su Secret <c>erp-audit-signature</c> firma con sus claves y deja de aceptar las de
/// desarrollo, cuyos secretos están en el repositorio. Se prueba con el enlazador de configuración real, porque es él
/// quien agrega a la lista que ya trae las de desarrollo: sin quitarlas, un PDF o un ancla firmados con
/// <c>dev-v1</c> o <c>dev-anclas-v1</c> seguirían verificando.
/// </summary>
public class ClavesDeFirmaPorAmbienteTests
{
    private const string PdfPropia = "erp-qa-pdf-202609";
    private const string AnclasPropia = "erp-qa-anclas-202609";

    private static IOptions<AuditSignatureSettings> Opciones(Dictionary<string, string?> valores)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var servicios = new ServiceCollection();
        servicios.AddAuditServices(configuracion);
        return servicios.BuildServiceProvider().GetRequiredService<IOptions<AuditSignatureSettings>>();
    }

    private static Dictionary<string, string?> ConSecret() => new()
    {
        ["AuditSignature:CurrentKeyVersion"] = PdfPropia,
        ["AuditSignature:AnchorKeyVersion"] = AnclasPropia,
        ["AuditSignature:Keys:0:Version"] = PdfPropia,
        ["AuditSignature:Keys:0:SecretBase64"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
        ["AuditSignature:Keys:1:Version"] = AnclasPropia,
        ["AuditSignature:Keys:1:SecretBase64"] = Convert.ToBase64String(Enumerable.Range(33, 32).Select(i => (byte)i).ToArray()),
    };

    [Fact]
    public void Con_claves_propias_las_de_desarrollo_salen_de_la_lista()
    {
        var s = Opciones(ConSecret()).Value;

        s.Keys.Select(k => k.Version).Should().BeEquivalentTo([PdfPropia, AnclasPropia]);
        s.UsaClavesDeDesarrollo.Should().BeFalse();
    }

    [Fact]
    public void Con_claves_propias_un_pdf_firmado_con_dev_v1_no_verifica()
    {
        var desarrollo = new AuditSignatureService(Options.Create(new AuditSignatureSettings()));
        var payload = "pdf de auditoría"u8.ToArray();
        var firmaConDev = desarrollo.ComputeHmacBase64(payload);

        IAuditSignatureService propio = new AuditSignatureService(Opciones(ConSecret()));

        propio.VerifyHmacBase64(payload, firmaConDev, "dev-v1").Should().BeFalse();
        propio.CurrentKeyVersion.Should().Be(PdfPropia);
        propio.AnchorKeyVersion.Should().Be(AnclasPropia);
        propio.VerifyHmacBase64(payload, propio.ComputeHmacBase64(payload), PdfPropia).Should().BeTrue();
    }

    [Fact]
    public void Sin_claves_propias_quedan_las_de_desarrollo()
    {
        var s = Opciones([]).Value;

        s.Keys.Select(k => k.Version).Should().BeEquivalentTo(["dev-v1", "dev-anclas-v1"]);
        s.UsaClavesDeDesarrollo.Should().BeTrue();
    }

    /// <summary>
    /// Incidente del 2026-10-26: la versión de anclas del Secret de producción tenía 21 caracteres y la columna 20; el proceso
    /// arrancaba y cada ancla fallaba con 22001. Ahora no arranca, y el mensaje dice qué valor sobra y cuál es el máximo.
    /// </summary>
    [Theory]
    [InlineData("AuditSignature:CurrentKeyVersion", "CurrentKeyVersion")]
    [InlineData("AuditSignature:AnchorKeyVersion", "AnchorKeyVersion")]
    [InlineData("AuditSignature:Keys:1:Version", "Keys:1:Version")]
    public void Una_version_mas_larga_que_su_columna_no_deja_arrancar(string clave, string nombre)
    {
        var larga = new string('v', AuditAnchor.LongitudMaximaDeVersionDeClave + 1);
        var valores = ConSecret();
        valores[clave] = larga;

        var leer = () => Opciones(valores).Value;

        leer.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain(nombre).And.Contain(larga)
            .And.Contain($"máximo es {AuditAnchor.LongitudMaximaDeVersionDeClave}").And.Contain("sobran 1");
    }

    [Fact]
    public void Una_version_del_largo_maximo_cabe()
    {
        var justa = new string('v', AuditAnchor.LongitudMaximaDeVersionDeClave);
        var valores = ConSecret();
        valores["AuditSignature:AnchorKeyVersion"] = justa;
        valores["AuditSignature:Keys:1:Version"] = justa;

        Opciones(valores).Value.AnchorKeyVersion.Should().Be(justa);
    }

    [Fact]
    public void La_validacion_se_hace_al_arrancar()
    {
        var servicios = new ServiceCollection();
        servicios.AddAuditServices(new ConfigurationBuilder().Build());

        servicios.Should().Contain(d => d.ServiceType == typeof(IValidateOptions<AuditSignatureSettings>)
                                        && d.ImplementationType == typeof(AuditSignatureSettingsValidator));
        servicios.Should().Contain(d => d.ServiceType == typeof(IStartupValidator), "ValidateOnStart registra la validación del arranque");
    }

    [Fact]
    public void La_columna_y_la_validacion_leen_el_mismo_maximo()
    {
        AuditAnchor.LongitudMaximaDeVersionDeClave.Should().Be(64);
        new AuditSignatureSettings().VersionesQueNoCaben().Should().BeEmpty("las claves de desarrollo caben");
    }
}
