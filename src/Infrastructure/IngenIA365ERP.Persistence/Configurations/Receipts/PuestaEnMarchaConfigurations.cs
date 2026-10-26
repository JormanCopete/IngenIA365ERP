using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US13 (T0286, T0291; data-model §15.2, §15.3): los cuadres de apertura y las cifras de referencia de Recaudo, en el par
// RecaudoYTesoreriaPuestaEnMarcha. Los de Tesorería tienen la misma forma (las bases de Domain/Common/Cuadres) y su configuración vive en
// Configurations/Treasury/Activation, sobre lo común de abajo, para que ninguna carpeta nombre las tablas del otro módulo (RecaudoYTesoreriaSeHablanPorSusPuertos;
// corrida del 2026-10-26). FK Restrict, nada entre las tablas de los dos módulos.

/// <summary>Lo común de la cabecera de un cuadre. (nuevo)</summary>
internal static class ConfiguracionDeCuadres
{
    public static void Cabecera<T>(EntityTypeBuilder<T> builder, string tabla) where T : CuadreDeAperturaBase
    {
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<T>().WithMany().HasForeignKey(e => e.DefinitiveSquareId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.TotalLoaded).Monto();
        builder.Property(e => e.TotalLedger).Monto();
        builder.Property(e => e.TotalDifference).Monto();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasIndex(e => new { e.Kind, e.Status }).HasDatabaseName($"IX_{tabla}_Kind_Status");
    }

    public static void Linea<TLinea, TCuadre>(EntityTypeBuilder<TLinea> builder, string tabla) where TLinea : LineaDeCuadreDeAperturaBase where TCuadre : CuadreDeAperturaBase
    {
        builder.HasOne<TCuadre>().WithMany().HasForeignKey(e => e.OpeningSquareId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Concept).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Dimension).HasMaxLength(40).IsRequired();
        builder.Property(e => e.DimensionLabel).HasMaxLength(150);
        builder.Property(e => e.Loaded).Monto();
        builder.Property(e => e.Ledger).Monto();
        builder.Property(e => e.Difference).Monto();
        builder.HasIndex(e => new { e.OpeningSquareId, e.Concept, e.Dimension }).IsUnique().HasDatabaseName($"UK_{tabla}_Square_Concept_Dimension");
    }

    public static void Cifra<T>(EntityTypeBuilder<T> builder, string tabla) where T : CifraDeReferenciaBase
    {
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LoadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Concept).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Dimension).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.SourceFileName).HasMaxLength(200);
        builder.HasIndex(e => new { e.CutoffDate, e.Concept, e.Dimension }).HasDatabaseName($"IX_{tabla}_Cutoff_Concept_Dimension");
    }
}

/// <summary><c>RCD_OpeningSquares</c>. (nuevo)</summary>
public class ReceiptsOpeningSquareConfiguration : IEntityTypeConfiguration<ReceiptsOpeningSquare>
{
    public const string Tabla = "RCD_OpeningSquares";

    public void Configure(EntityTypeBuilder<ReceiptsOpeningSquare> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        ConfiguracionDeCuadres.Cabecera(builder, Tabla);
    }
}

/// <summary><c>RCD_OpeningSquareLines</c> (H). (nuevo)</summary>
public class ReceiptsOpeningSquareLineConfiguration : IEntityTypeConfiguration<ReceiptsOpeningSquareLine>
{
    public const string Tabla = "RCD_OpeningSquareLines";

    public void Configure(EntityTypeBuilder<ReceiptsOpeningSquareLine> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        ConfiguracionDeCuadres.Linea<ReceiptsOpeningSquareLine, ReceiptsOpeningSquare>(builder, Tabla);
    }
}

/// <summary><c>RCD_ReferenceFigures</c>. (nuevo)</summary>
public class ReceiptsReferenceFigureConfiguration : IEntityTypeConfiguration<ReceiptsReferenceFigure>
{
    public const string Tabla = "RCD_ReferenceFigures";

    public void Configure(EntityTypeBuilder<ReceiptsReferenceFigure> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        ConfiguracionDeCuadres.Cifra(builder, Tabla);
    }
}
