using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Treasury.Common;

/// <summary>
/// Feature 015, T0018 (R-13; FR-007): <see cref="NumeradorDeTesoreria"/> numera sobre <c>COR_Sequences</c> con la clave <c>TES:{código}</c>, sin
/// huecos y al final; la fila que falta la pide al cerrojo de Tesorería.
/// </summary>
public class NumeradorDeTesoreriaTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICerrojoDeTesoreria _cerrojo = Substitute.For<ICerrojoDeTesoreria>();

    [Fact]
    public void La_clave_es_el_modulo_y_el_codigo() => NumeradorDeTesoreria.Clave("LT").Should().Be("TES:LT");

    [Fact]
    public async Task Numera_e_incrementa_y_bloquea_la_fila_existente()
    {
        var s = new Sequence { Name = "TES:LT", ShortName = "LT-", DocumentType = "TES", IsAutomatic = true, NextSequence = 7 };
        _db.Sequences.Add(s);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        (await new NumeradorDeTesoreria(_db, _cerrojo).AsignarAsync("LT", "LT-")).Should().Be("LT-000007");
        await _db.SaveChangesAsync();

        (await _db.Sequences.SingleAsync(x => x.Id == s.Id)).NextSequence.Should().Be(8);
        await _cerrojo.Received(1).BloquearNumeracionAsync(s.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_primera_vez_pide_la_fila_al_cerrojo()
    {
        (await new NumeradorDeTesoreria(_db, _cerrojo).AsignarAsync("CHQ", "CH-")).Should().Be("CH-000001");
        await _cerrojo.Received(1).AsegurarNumeracionAsync("TES:CHQ", "CH-", Arg.Any<CancellationToken>());
    }
}
