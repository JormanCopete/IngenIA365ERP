using FluentAssertions;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Exceptions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, I4 (T691; data-model §18 y §26, duda 5): un hecho inmutable admite una sola forma de <c>Modified</c>: que
/// toda propiedad modificada lleve <c>[EscrituraUnica]</c> y su valor original sea nulo (los artefactos de la versión del
/// documento electrónico llegan después del commit). Cualquier otro cambio o borrado sigue rechazado. Se prueba sobre un
/// contexto propio con un hecho de prueba, para no depender de la configuración EF de la versión.
/// </summary>
public class EscrituraUnicaEnHechosTests
{
    private sealed class HechoConArtefacto : BaseEntity, IHechoInmutable
    {
        public string Texto { get; init; } = string.Empty;

        [EscrituraUnica]
        public Guid? ArtefactoPublicId { get; private set; }

        [EscrituraUnica]
        public Guid? OtroArtefactoPublicId { get; private set; }

        public void Fijar(Guid? artefacto) => ArtefactoPublicId = artefacto;
        public void FijarOtro(Guid? artefacto) => OtroArtefactoPublicId = artefacto;
    }

    private sealed class Contexto(DbContextOptions<Contexto> opciones) : Microsoft.EntityFrameworkCore.DbContext(opciones)
    {
        public DbSet<HechoConArtefacto> Hechos => Set<HechoConArtefacto>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HechoConArtefacto>().Ignore(h => h.DomainEvents);
            modelBuilder.Entity<HechoConArtefacto>().Property(h => h.RowVersion).IsRequired(false);
        }
    }

    private readonly Contexto _db = new(new DbContextOptionsBuilder<Contexto>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private HechoConArtefacto Guardado(Guid? artefacto = null)
    {
        var hecho = new HechoConArtefacto { Texto = "v1" };
        hecho.Fijar(artefacto);
        _db.Hechos.Add(hecho);
        _db.SaveChanges();
        return hecho;
    }

    private Task Verificar() => GuardaDeInmutabilidad.VerificarAsync(_db);

    [Fact]
    public async Task Nulo_a_valor_en_una_columna_de_escritura_unica_se_admite()
    {
        var hecho = Guardado();
        hecho.Fijar(Guid.NewGuid());
        hecho.FijarOtro(Guid.NewGuid());

        await Verificar();
    }

    [Fact]
    public async Task Un_valor_ya_escrito_no_se_reemplaza_ni_se_vacia()
    {
        var hecho = Guardado(Guid.NewGuid());
        hecho.Fijar(Guid.NewGuid());
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(HechoConArtefacto.ArtefactoPublicId));

        hecho.Fijar(null);
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(HechoConArtefacto.ArtefactoPublicId));
    }

    [Fact]
    public async Task Otra_columna_modificada_junto_con_la_de_escritura_unica_se_rechaza()
    {
        var hecho = Guardado();
        hecho.Fijar(Guid.NewGuid());
        _db.Entry(hecho).Property(h => h.Texto).CurrentValue = "v2";

        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(HechoConArtefacto.Texto));
    }

    [Fact]
    public async Task Un_hecho_con_escritura_unica_tampoco_se_borra()
    {
        var hecho = Guardado();
        _db.Entry(hecho).State = EntityState.Deleted;

        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.EntityType.Should().Be(nameof(HechoConArtefacto));
    }
}
