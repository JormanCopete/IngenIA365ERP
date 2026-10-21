using FluentAssertions;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Application.Tests.Common.Parameters;

/// <summary>
/// T0027 (feature 013; decisiones-transversales T38; quickstart §1.5 «Parámetros»): <c>LectorDeParametros</c> con
/// <c>informacionHasta</c> (R). Regla propia de <c>COR_ParameterVersions</c>: R se compara con <c>CreatedAt</c>, el <c>ValidTo</c>
/// guardado se ignora y se deriva de la vigencia siguiente conocida en R, y una baja posterior a R no cuenta. Sin R, el lector
/// responde exactamente como hoy (además corren sin cambios las pruebas de la 012, <c>LectorDeParametrosTests</c>).
/// </summary>
public class LectorDeParametrosConInformacionHastaTests
{
    private const string Inv = ParametrosDeInventario.Modulo;
    private const string Dias = ParametrosDeInventario.ComprasDiasAlertaEventosRadian; // entero, defecto 3

    private static readonly DateTime Enero = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Febrero = new(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Marzo = new(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Abril = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Julio = new(2026, 7, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private ParameterVersion Guardar(string valor, DateOnly desde, DateOnly? hasta, DateTime creadaEn,
        ParameterScopeKind ambito = ParameterScopeKind.None, int ambitoId = 0, DateTime? bajaEn = null, string clave = Dias)
    {
        var v = new ParameterVersion
        {
            Module = Inv, Key = clave, ScopeKind = ambito, ScopeId = ambitoId, Value = valor, ValidFrom = desde, ValidTo = hasta,
            Reason = "prueba", CreatedAt = creadaEn, IsDeleted = bajaEn is not null, DeletedAt = bajaEn,
        };
        _db.ParameterVersions.Add(v);
        _db.SaveChanges();
        return v;
    }

    /// <summary>
    /// Enero: «5» desde el 1 de enero. Marzo: «8» desde el 1 de junio, y el alta cerró la de enero el 31 de mayo (reescribió su ValidTo).
    /// </summary>
    private void DosVigencias()
    {
        Guardar("5", new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31), Enero);
        Guardar("8", new DateOnly(2026, 6, 1), null, Marzo);
    }

    [Fact]
    public async Task Una_vigencia_creada_despues_de_R_no_se_ve()
    {
        DosVigencias();

        var r = await new LectorDeParametros(_db).LeerAsync(Inv, Dias, Julio, Febrero);

        r.Value.Como<int>().Should().Be(5, "en febrero sólo se conocía la vigencia de enero");
    }

    [Fact]
    public async Task El_ValidTo_guardado_se_ignora_y_se_deriva_del_sucesor_conocido_en_R()
    {
        DosVigencias();

        // El ValidTo guardado (31 de mayo) lo escribió el alta de marzo: en febrero la de enero seguía abierta.
        (await new LectorDeParametros(_db).LeerAsync(Inv, Dias, Julio, Febrero)).Value.Vigencia!.ValidTo.Should().BeNull();

        // En abril ya se conocía la de junio: la de enero termina la víspera y en julio rige «8».
        var abril = await new LectorDeParametros(_db).LeerAsync(Inv, Dias, Julio, Abril);
        abril.Value.Como<int>().Should().Be(8);
        (await new LectorDeParametros(_db).LeerAsync(Inv, Dias, new DateOnly(2026, 5, 31), Abril)).Value.Vigencia!.ValidTo
            .Should().Be(new DateOnly(2026, 5, 31));
    }

    [Fact]
    public async Task Antes_de_que_existiera_toda_vigencia_rige_el_defecto()
    {
        DosVigencias();

        var r = await new LectorDeParametros(_db).LeerAsync(Inv, Dias, Julio, new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        r.Value.EsDefecto.Should().BeTrue();
        r.Value.Como<int>().Should().Be(3);
    }

    [Fact]
    public async Task Una_baja_posterior_a_R_no_cuenta()
    {
        Guardar("5", new DateOnly(2026, 1, 1), null, Enero);
        Guardar("9", new DateOnly(2026, 6, 1), null, Febrero, bajaEn: Marzo);

        // En febrero se conocía la de junio y nadie la había retirado: rige.
        (await new LectorDeParametros(_db).LeerComoAsync<int>(Inv, Dias, Julio)).Value.Should().Be(5, "hoy está retirada");
        (await new LectorDeParametros(_db).LeerAsync(Inv, Dias, Julio, Febrero)).Value.Como<int>().Should().Be(9);
        (await new LectorDeParametros(_db).LeerAsync(Inv, Dias, Julio, Abril)).Value.Como<int>().Should().Be(5, "en abril ya estaba retirada");
    }

    [Fact]
    public async Task Cada_ambito_deriva_su_fin_de_su_propio_sucesor()
    {
        const string unidades = ParametrosDeInventario.ConteoToleranciaReconteoUnidades; // admite la bodega
        Guardar("5", new DateOnly(2026, 1, 1), null, Enero, clave: unidades);
        Guardar("6", new DateOnly(2026, 1, 1), null, Enero, ParameterScopeKind.Warehouse, 7, clave: unidades);
        Guardar("8", new DateOnly(2026, 6, 1), null, Marzo, clave: unidades);

        var bodega = await new LectorDeParametros(_db).LeerAsync(Inv, unidades, Julio, Abril, ParameterScopeKind.Warehouse, 7);
        var general = await new LectorDeParametros(_db).LeerAsync(Inv, unidades, Julio, Abril);

        bodega.Value.Como<decimal>().Should().Be(6m, "la vigencia general nueva no cierra la excepción de la bodega");
        general.Value.Como<decimal>().Should().Be(8m);
    }

    [Fact]
    public async Task Sin_informacionHasta_el_resultado_es_el_de_siempre()
    {
        DosVigencias();
        Guardar("2", new DateOnly(2026, 8, 1), null, Abril, bajaEn: Abril);

        foreach (var fecha in new[] { new DateOnly(2025, 12, 31), new DateOnly(2026, 3, 1), Julio, new DateOnly(2026, 9, 1) })
        {
            var antes = await new LectorDeParametros(_db).LeerAsync(Inv, Dias, fecha);
            var conNulo = await new LectorDeParametros(_db).LeerAsync(Inv, Dias, fecha, (DateTime?)null);
            conNulo.Value.Texto.Should().Be(antes.Value.Texto);
            conNulo.Value.Vigencia.Should().Be(antes.Value.Vigencia, "con R nulo se leen las vigencias guardadas, con su ValidTo");
        }
    }
}
