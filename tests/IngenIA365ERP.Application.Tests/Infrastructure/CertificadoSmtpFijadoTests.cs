using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Fijación del certificado del servidor de correo.
///
/// <para>
/// Es la frontera de seguridad del correo saliente. El servidor real presenta un
/// certificado autofirmado, y la salida fácil —desactivar la validación— acepta
/// CUALQUIER certificado: quien se interponga en la red podría descifrar el
/// tráfico y quedarse con los enlaces de invitación y de restablecimiento de
/// contraseña que viajan en esos correos. Fijar la huella acepta uno y sólo uno.
/// </para>
///
/// <para>
/// Si alguna de estas pruebas se pone en verde relajando la comparación, el
/// parche deja de ser un parche y pasa a ser un agujero.
/// </para>
/// </summary>
public class CertificadoSmtpFijadoTests
{
    private static X509Certificate2 CertificadoDePrueba(string nombre)
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(
            $"CN={nombre}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return solicitud.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static string Huella(X509Certificate2 cert) =>
        Convert.ToHexString(SHA256.HashData(cert.RawData));

    private static SmtpEmailSender Emisor(string? huellaAceptada)
    {
        var opciones = Options.Create(new SmtpSettings
        {
            Host = "mail.ejemplo.test",
            HuellaCertificadoAceptada = huellaAceptada,
        });
        return new SmtpEmailSender(opciones, NullLogger<SmtpEmailSender>.Instance);
    }

    [Fact]
    public void CertificadoValido_SeAceptaAunqueNoCoincidaLaHuella()
    {
        // Si el servidor arregla su certificado, no hay que tocar configuración
        // para que siga funcionando.
        using var cert = CertificadoDePrueba("otro.ejemplo.test");
        var emisor = Emisor("AABBCCDD");

        emisor.ValidarCertificadoFijado(this, cert, null, SslPolicyErrors.None)
            .Should().BeTrue();
    }

    [Fact]
    public void CertificadoInvalido_ConHuellaQueCoincide_SeAcepta()
    {
        using var cert = CertificadoDePrueba("mail.ejemplo.test");
        var emisor = Emisor(Huella(cert));

        emisor.ValidarCertificadoFijado(
            this, cert, null, SslPolicyErrors.RemoteCertificateChainErrors)
            .Should().BeTrue();
    }

    [Fact]
    public void CertificadoInvalido_ConHuellaDISTINTA_SeRechaza()
    {
        // El caso que importa: alguien se interpone y presenta OTRO certificado.
        using var elBueno = CertificadoDePrueba("mail.ejemplo.test");
        using var elDelAtacante = CertificadoDePrueba("mail.ejemplo.test");
        var emisor = Emisor(Huella(elBueno));

        emisor.ValidarCertificadoFijado(
            this, elDelAtacante, null, SslPolicyErrors.RemoteCertificateChainErrors)
            .Should().BeFalse("el mismo nombre no basta: la huella es distinta");
    }

    [Fact]
    public void CertificadoInvalido_SinHuellaConfigurada_SeRechaza()
    {
        // Sin huella configurada este callback ni siquiera se engancha, pero si
        // alguien lo llamara igual, no debe aceptar nada.
        using var cert = CertificadoDePrueba("mail.ejemplo.test");
        var emisor = Emisor(null);

        emisor.ValidarCertificadoFijado(
            this, cert, null, SslPolicyErrors.RemoteCertificateNameMismatch)
            .Should().BeFalse();
    }

    [Fact]
    public void SinCertificado_SeRechaza()
    {
        var emisor = Emisor("AABBCCDD");

        emisor.ValidarCertificadoFijado(
            this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("con-minusculas")]
    [InlineData("con:dos:puntos")]
    [InlineData("con espacios")]
    public void LaHuellaSeCompara_IgnorandoFormato(string variante)
    {
        // Las herramientas muestran la huella de tres formas distintas. Copiarla
        // de openssl y que no funcione por los dos puntos seria una hora perdida.
        using var cert = CertificadoDePrueba("mail.ejemplo.test");
        var huella = Huella(cert);

        var formateada = variante switch
        {
            "con-minusculas" => huella.ToLowerInvariant(),
            "con:dos:puntos" => string.Join(":", Enumerable.Range(0, huella.Length / 2)
                .Select(i => huella.Substring(i * 2, 2))),
            _ => string.Join(" ", Enumerable.Range(0, huella.Length / 2)
                .Select(i => huella.Substring(i * 2, 2))),
        };

        Emisor(formateada).ValidarCertificadoFijado(
            this, cert, null, SslPolicyErrors.RemoteCertificateChainErrors)
            .Should().BeTrue();
    }
}
