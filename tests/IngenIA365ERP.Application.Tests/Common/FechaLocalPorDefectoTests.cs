using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.Application.Tests.Common;

/// <summary>
/// T20 / T038: <see cref="IDateTimeService"/> gana <c>AhoraLocal</c> y <c>HoyLocal</c> con una
/// implementación por defecto sobre <c>UtcNow</c> y −05:00, para que ningún reloj falso existente
/// tenga que cambiar. La API los implementa de verdad con la zona configurada.
/// </summary>
public class FechaLocalPorDefectoTests
{
    private sealed class RelojSoloUtc(DateTime utc) : IDateTimeService
    {
        public DateTime UtcNow => utc;
        public DateOnly TodayUtc => DateOnly.FromDateTime(utc);
    }

    [Fact]
    public void A_las_dos_de_la_manana_UTC_en_Colombia_todavia_es_el_dia_anterior()
    {
        IDateTimeService reloj = new RelojSoloUtc(new DateTime(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc));

        reloj.TodayUtc.Should().Be(new DateOnly(2026, 9, 25));
        reloj.HoyLocal.Should().Be(new DateOnly(2026, 9, 24), "la fecha de operación es la local, no la UTC");
        reloj.AhoraLocal.Offset.Should().Be(TimeSpan.FromHours(-5));
        reloj.AhoraLocal.UtcDateTime.Should().Be(new DateTime(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc));
        reloj.AhoraLocal.Hour.Should().Be(21);
    }

    [Fact]
    public void A_mediodia_coinciden()
    {
        IDateTimeService reloj = new RelojSoloUtc(new DateTime(2026, 9, 25, 17, 0, 0, DateTimeKind.Utc));

        reloj.HoyLocal.Should().Be(reloj.TodayUtc);
    }

    [Fact]
    public void Un_UtcNow_sin_Kind_se_toma_como_UTC()
    {
        IDateTimeService reloj = new RelojSoloUtc(new DateTime(2026, 1, 1, 3, 0, 0, DateTimeKind.Unspecified));

        reloj.HoyLocal.Should().Be(new DateOnly(2025, 12, 31));
    }
}
