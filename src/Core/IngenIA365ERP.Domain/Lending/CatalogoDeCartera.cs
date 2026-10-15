namespace IngenIA365ERP.Domain.Lending;

/// <summary>
/// Las entregas de Cartera (feature 013, R15, T43; decisiones-transversales §2.7 y §6; data-model §9: «no se guarda»), en el
/// orden en que salen. Precedente: <c>EntregaDelComercio</c> de la feature 012. Se convierte sola a
/// <c>EntregaDeModulo("LND", n)</c>, que es lo que declaran las claves de <c>ParametrosDeCartera</c>.
/// </summary>
public enum EntregaDeCartera
{
    K1 = 1,
    K2 = 2,
    K3 = 3,
    K4 = 4,
    K5 = 5,
    K6a = 6,
    K6b = 7,
}

/// <summary>
/// Lo que la versión desplegada del programa sabe de Cartera (feature 013, T43, R15). <see cref="EntregaVigente"/> es la entrega
/// en producción: una clave de parámetro, una plantilla, un tipo de alerta o una clase de documento de una entrega posterior no
/// se ofrece ni se acepta todavía (<c>CadaParametroRespetaLaEntregaDeSuModulo</c>). La sube la entrega que habilita lo suyo
/// (T0849 a K2, T0889 a K3, T0955 a K4, T0969 a K5, T1024 a K6a, T1058 a K6b). (nuevo)
/// </summary>
public static class CatalogoDeCartera
{
    /// <summary>El módulo de Cartera en los catálogos de plataforma (<c>COR_ParameterVersions.Module</c>, tipos de alerta).</summary>
    public const string Modulo = "LND";

    /// <summary>La entrega de Cartera en producción en esta versión del programa.</summary>
    public const EntregaDeCartera EntregaVigente = EntregaDeCartera.K3;
}
