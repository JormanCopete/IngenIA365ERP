using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Domain.Tests.Entities.Admin;

/// <summary>Feature 027: el pase de ingreso sin segundo factor y su lugar en la política de la cooperativa.</summary>
public sealed class LoginPassTests
{
    private static readonly DateTime Ahora = new(2026, 11, 26, 13, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Huella = new byte[32];

    private static LoginPass Pase(TimeSpan? duracion = null) => LoginPass.Conceder(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), grantedByMaster: false, "Dejó el celular en casa", Huella, "sello", Ahora,
        duracion ?? TimeSpan.FromHours(12));

    [Fact]
    public void Vence_a_las_horas_pedidas_y_sirve_varias_veces_mientras_tanto()
    {
        var pase = Pase();
        pase.ExpiresAt.Should().Be(Ahora.AddHours(12));
        pase.RegistrarUso(Ahora.AddHours(1), "10.0.0.1");
        pase.RegistrarUso(Ahora.AddHours(5), "10.0.0.2");
        pase.UseCount.Should().Be(2);
        pase.LastUsedIp.Should().Be("10.0.0.2");
        pase.Estado(Ahora.AddHours(11)).Should().Be(EstadoDelPase.Active);
        pase.Estado(Ahora.AddHours(12)).Should().Be(EstadoDelPase.Expired);
        FluentActions.Invoking(() => pase.RegistrarUso(Ahora.AddHours(12).AddMinutes(1), null)).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    public void No_dura_mas_de_una_jornada(int horas) =>
        FluentActions.Invoking(() => Pase(TimeSpan.FromHours(horas))).Should().Throw<ArgumentOutOfRangeException>();

    [Theory]
    [InlineData("corto")]
    [InlineData("")]
    public void Exige_motivo(string motivo) =>
        FluentActions.Invoking(() => LoginPass.Conceder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false, motivo, Huella, null, Ahora,
            TimeSpan.FromHours(1))).Should().Throw<ArgumentException>();

    [Fact]
    public void Anular_es_idempotente_y_conserva_el_primer_motivo()
    {
        var pase = Pase();
        pase.Anular(Ahora.AddHours(1), LoginPass.Motivos.EntroConSegundoFactor, null);
        pase.Anular(Ahora.AddHours(2), LoginPass.Motivos.RevocadoPorAdministrador, Guid.NewGuid());
        pase.VoidReason.Should().Be(LoginPass.Motivos.EntroConSegundoFactor);
        pase.VoidedAt.Should().Be(Ahora.AddHours(1));
        pase.EstaVigente(Ahora.AddHours(1)).Should().BeFalse();
        pase.Estado(Ahora.AddHours(1)).Should().Be(EstadoDelPase.Voided);
    }

    [Fact]
    public void La_politica_viene_apagada_con_doce_horas_y_no_admite_mas()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());
        politica.AllowLoginPass.Should().BeFalse();
        politica.LoginPassMaxHours.Should().Be(12);
        politica.ConfigurarPasesDeIngreso(true, 8);
        politica.AllowLoginPass.Should().BeTrue();
        politica.LoginPassMaxHours.Should().Be(8);
        FluentActions.Invoking(() => politica.ConfigurarPasesDeIngreso(true, 13)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => politica.ConfigurarPasesDeIngreso(true, 0)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
