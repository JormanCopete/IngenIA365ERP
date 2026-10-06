namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// Los tipos de alerta que levantan la réplica y los lectores de valores nacionales (feature 013; data-model parte 3 §11) y
/// sus condiciones (<c>DedupKey</c>). Los textos son los códigos del catálogo cerrado <c>TiposDeAlerta</c>, que los registra
/// con su módulo, destinatarios y canal (T0091); mientras no estén allí, <c>IAlertas</c> responde
/// <c>Alerts.Type.NotFound</c> y quien levanta lo anota en el log sin fallar. (nuevo)
/// </summary>
public static class AlertasDeValoresNacionales
{
    public const string ValorNacionalFaltante = "Cartera.ValorNacionalFaltante";
    public const string ReplicaAtrasada = "Cartera.ReplicaDeValoresNacionalesAtrasada";
    public const string IndiceDesactualizado = "Cartera.IndiceDesactualizado";
    public const string NormativaPendienteDeValidar = "Cartera.NormativaPendienteDeValidar";
    public const string LimiteLegalMasEstricto = "Cartera.LimiteLegalMasEstricto";
    public const string ValorNacionalDistinto = "Nomina.ValorNacionalDistinto";

    /// <summary><c>Cartera.ValorNacionalFaltante:{código}:{clave}:{fecha}</c>.</summary>
    public static string ClaveDeFaltante(string codigo, string clave, DateOnly fecha) =>
        $"{ValorNacionalFaltante}:{codigo}:{clave}:{fecha:yyyy-MM-dd}";

    /// <summary><c>Cartera.IndiceDesactualizado:{código}:{clave}</c>.</summary>
    public static string ClaveDeIndiceDesactualizado(string codigo, string clave) => $"{IndiceDesactualizado}:{codigo}:{clave}";

    /// <summary><c>Cartera.LimiteLegalMasEstricto:{límite}</c>.</summary>
    public static string ClaveDeLimite(string limite) => $"{LimiteLegalMasEstricto}:{limite}";

    /// <summary><c>Nomina.ValorNacionalDistinto:{código}:{fecha}</c>.</summary>
    public static string ClaveDeDistinto(string codigo, DateOnly fecha) => $"{ValorNacionalDistinto}:{codigo}:{fecha:yyyy-MM-dd}";

    /// <summary>La réplica atrasada no lleva entidad: una por cooperativa.</summary>
    public const string ClaveDeReplicaAtrasada = ReplicaAtrasada;
}
