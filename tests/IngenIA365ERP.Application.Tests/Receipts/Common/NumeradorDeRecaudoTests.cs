using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Common;

/// <summary>
/// Feature 015, T0018 (R-13; FR-007, FR-026): <see cref="NumeradorDeRecaudo"/> numera sobre <c>COR_Sequences</c> con la clave
/// <c>RCD:{código}:{prefijo}</c>, sin huecos, al final (bloquea la fila existente con el último nivel del cerrojo), crea la fila que falta por el
/// cerrojo y un rechazo no consume número: lo que no se guarda no avanza el consecutivo. Molde: <c>NumeradorDeCarteraTests</c>.
/// </summary>
public class NumeradorDeRecaudoTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICerrojoDeRecaudo _cerrojo = Substitute.For<ICerrojoDeRecaudo>();

    private NumeradorDeRecaudo Numerador() => new(_db, _cerrojo);

    private Sequence Secuencia(string clave, string prefijo, long siguiente, string modulo = "RCD")
    {
        var s = new Sequence { Name = clave, ShortName = prefijo, DocumentType = modulo, IsAutomatic = true, NextSequence = siguiente };
        _db.Sequences.Add(s);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return s;
    }

    [Fact]
    public void La_clave_es_modulo_codigo_y_prefijo() => NumeradorDeRecaudo.Clave("RC", "RC-").Should().Be("RCD:RC:RC-");

    [Fact]
    public async Task Numera_con_su_prefijo_e_incrementa_bloqueando_la_fila_al_final()
    {
        var s = Secuencia("RCD:RC:RC-", "RC-", 41);

        (await Numerador().AsignarAsync("RC", "RC-")).Should().Be("RC-000041");
        (await Numerador().AsignarAsync("RC", "RC-")).Should().Be("RC-000042");
        await _db.SaveChangesAsync();

        (await _db.Sequences.SingleAsync(x => x.Id == s.Id)).NextSequence.Should().Be(43);
        await _cerrojo.Received(1).BloquearNumeracionAsync(s.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_primera_vez_pide_al_cerrojo_la_fila_y_sin_ella_la_agrega()
    {
        (await Numerador().AsignarAsync("AN", "AN-")).Should().Be("AN-000001");
        (await Numerador().AsignarAsync("AN", "AN-")).Should().Be("AN-000002", "la fila recién creada sigue en la misma unidad");
        await _db.SaveChangesAsync();

        await _cerrojo.Received(1).AsegurarNumeracionAsync("RCD:AN:AN-", "AN-", Arg.Any<CancellationToken>());
        (await _db.Sequences.SingleAsync(x => x.Name == "RCD:AN:AN-")).DocumentType.Should().Be("RCD");
    }

    [Fact]
    public async Task Cada_tipo_y_prefijo_lleva_su_propio_consecutivo_y_no_toca_otros_modulos()
    {
        Secuencia("RCD:RC:RC-", "RC-", 10);
        Secuencia("RCD:RC:RC-", "RC-", 900, modulo: "INV");

        (await Numerador().AsignarAsync("RC", "RC-")).Should().Be("RC-000010");
        (await Numerador().AsignarAsync("RC", "RB-")).Should().Be("RB-000001");
    }

    [Fact]
    public async Task Un_rechazo_no_consume_numero()
    {
        var s = Secuencia("RCD:RC:RC-", "RC-", 5);

        await Numerador().AsignarAsync("RC", "RC-");
        _db.ChangeTracker.Clear();   // la transacción se deshizo: nada se guardó

        (await _db.Sequences.SingleAsync(x => x.Id == s.Id)).NextSequence.Should().Be(5);
        (await new NumeradorDeRecaudo(_db, _cerrojo).AsignarAsync("RC", "RC-")).Should().Be("RC-000005");
    }
}
