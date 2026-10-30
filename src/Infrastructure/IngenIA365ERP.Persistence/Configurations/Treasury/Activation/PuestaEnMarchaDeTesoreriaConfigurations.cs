using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Persistence.Configurations.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.Activation;

// Feature 015, US13 (T0286, T0291; data-model §15.2, §15.3): los cuadres de apertura y las cifras de referencia de Tesorería, en el par
// RecaudoYTesoreriaPuestaEnMarcha (los de Recaudo, en Configurations/Receipts). FK Restrict, nada entre las tablas de los dos módulos; lo común (ConfiguracionDeCuadres) es de Recaudo, en la misma forma.

/// <summary><c>TES_OpeningSquares</c>. (nuevo)</summary>
public class TreasuryOpeningSquareConfiguration : IEntityTypeConfiguration<TreasuryOpeningSquare>
{
    public const string Tabla = "TES_OpeningSquares";

    public void Configure(EntityTypeBuilder<TreasuryOpeningSquare> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        ConfiguracionDeCuadres.Cabecera(builder, Tabla);
    }
}

/// <summary><c>TES_OpeningSquareLines</c> (H). (nuevo)</summary>
public class TreasuryOpeningSquareLineConfiguration : IEntityTypeConfiguration<TreasuryOpeningSquareLine>
{
    public const string Tabla = "TES_OpeningSquareLines";

    public void Configure(EntityTypeBuilder<TreasuryOpeningSquareLine> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        ConfiguracionDeCuadres.Linea<TreasuryOpeningSquareLine, TreasuryOpeningSquare>(builder, Tabla);
    }
}

/// <summary><c>TES_ReferenceFigures</c>. (nuevo)</summary>
public class TreasuryReferenceFigureConfiguration : IEntityTypeConfiguration<TreasuryReferenceFigure>
{
    public const string Tabla = "TES_ReferenceFigures";

    public void Configure(EntityTypeBuilder<TreasuryReferenceFigure> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        ConfiguracionDeCuadres.Cifra(builder, Tabla);
    }
}
