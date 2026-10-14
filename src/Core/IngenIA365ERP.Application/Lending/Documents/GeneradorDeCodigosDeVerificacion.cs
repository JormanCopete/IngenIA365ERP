using System.Security.Cryptography;
using IngenIA365ERP.Domain.Lending.Documents;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>
/// La fuente de códigos de verificación de los documentos emitidos (feature 013, US13, T0784; adaptadores-y-archivos.md §8.4): el motor puro
/// <see cref="CodigoDeVerificacion"/> con 80 bits aleatorios. Es un puerto para que las pruebas fuercen una colisión. (nuevo)
/// </summary>
public interface IGeneradorDeCodigosDeVerificacion
{
    /// <summary>Un código nuevo de 16 caracteres Crockford base32 (la forma guardada, sin guiones).</summary>
    string Generar();
}

/// <summary>El generador de producción: <see cref="RandomNumberGenerator"/>. (nuevo)</summary>
public sealed class GeneradorDeCodigosDeVerificacion : IGeneradorDeCodigosDeVerificacion
{
    public string Generar() => CodigoDeVerificacion.Generar(RandomNumberGenerator.GetBytes);
}
