namespace IngenIA365ERP.Domain.Enums.Dian;

/// <summary>
/// Ambiente DIAN; los mismos códigos que el atributo <c>Ambiente</c> del XML. Nació en la nómina electrónica
/// (feature 010) y se trasladó aquí en la feature 012 (T689, T40) porque también lo usan la facturación electrónica,
/// sus resoluciones y su configuración. Mismos valores: la columna sigue siendo <c>int</c> y no hubo migración.
/// </summary>
public enum DianEnvironment
{
    Production = 1,
    Testing = 2,
}
