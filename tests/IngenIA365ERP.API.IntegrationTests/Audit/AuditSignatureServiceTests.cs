using FluentAssertions;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Services;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.IntegrationTests.Audit;

/// <summary>
/// Unit tests del firmador HMAC (T089). NO requiere Docker — vive en este
/// proyecto solo porque <c>IngenIA365ERP.Audit</c> es internamente
/// accesible desde aquí y no desde <c>Application.Tests</c>.
/// </summary>
public class AuditSignatureServiceTests
{
    private const string SecretBase64 = "dGVzdC1zZWNyZXQtMzItYnl0ZXMtZm9yLWhtYWMtc2hhMjU2LXh4";
    private const string OtherSecretBase64 = "Y29udHJhc2VuYS1hbHRlcm5hLXBhcmEtcm90YWNpb24tZGUtY2xhdmU=";

    private static AuditSignatureService BuildService(params (string Version, string Secret)[] keys)
    {
        var settings = new AuditSignatureSettings
        {
            CurrentKeyVersion = keys[0].Version,
            Keys = keys.Select(k => new AuditSignatureKey
            {
                Version = k.Version,
                SecretBase64 = k.Secret
            }).ToList()
        };
        return new AuditSignatureService(Options.Create(settings));
    }

    [Fact]
    public void Compute_and_verify_round_trip_succeeds_with_current_key()
    {
        var svc = BuildService(("v1", SecretBase64));
        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        var hmac = svc.ComputeHmacBase64(payload);
        svc.VerifyHmacBase64(payload, hmac, "v1").Should().BeTrue();
    }

    [Fact]
    public void Verify_fails_when_payload_was_tampered()
    {
        var svc = BuildService(("v1", SecretBase64));
        var original = new byte[] { 1, 2, 3, 4, 5 };
        var hmac = svc.ComputeHmacBase64(original);
        var tampered = new byte[] { 1, 2, 3, 4, 99 };

        svc.VerifyHmacBase64(tampered, hmac, "v1").Should().BeFalse();
    }

    [Fact]
    public void Verify_fails_when_key_version_is_unknown()
    {
        var svc = BuildService(("v1", SecretBase64));
        var payload = new byte[] { 1, 2, 3 };
        var hmac = svc.ComputeHmacBase64(payload);

        svc.VerifyHmacBase64(payload, hmac, "v999").Should().BeFalse();
    }

    [Fact]
    public void Verify_uses_correct_key_for_each_version()
    {
        var svc = BuildService(("v2", OtherSecretBase64), ("v1", SecretBase64));
        var payload = new byte[] { 9, 8, 7 };

        // El HMAC actual está firmado con v2.
        var currentHmac = svc.ComputeHmacBase64(payload);
        svc.VerifyHmacBase64(payload, currentHmac, "v2").Should().BeTrue();
        // ...y NO debe validar como si fuera v1 (clave distinta).
        svc.VerifyHmacBase64(payload, currentHmac, "v1").Should().BeFalse();
    }

    [Fact]
    public void Constructor_throws_when_current_key_version_not_in_keys_list()
    {
        var settings = new AuditSignatureSettings
        {
            CurrentKeyVersion = "v999",
            Keys = [new AuditSignatureKey { Version = "v1", SecretBase64 = SecretBase64 }]
        };

        var act = () => new AuditSignatureService(Options.Create(settings));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*v999*");
    }

    [Fact]
    public void Verify_fails_when_hmac_is_not_valid_base64()
    {
        var svc = BuildService(("v1", SecretBase64));
        svc.VerifyHmacBase64(new byte[] { 1 }, "!!!not-base64!!!", "v1").Should().BeFalse();
    }
}
