namespace IngenIA365ERP.Audit.Configuration;

/// <summary>
/// Cómo se llama la base de auditoría de cada cooperativa, y la global.
///
/// <para>
/// Un solo sitio, por la misma razón que la cadena de conexión: componer estos
/// nombres es lo que decide dónde acaba el rastro regulatorio, y tenerlo
/// repartido ya costó caro — convivían dos convenciones y la consola sólo leía
/// una, así que los eventos de identidad quedaban escritos e invisibles.
/// </para>
/// </summary>
public static class AuditDatabaseNames
{
    /// <summary>Colección única dentro de cada base. Ya no lleva sufijo: la base ya identifica la cooperativa.</summary>
    public const string Coleccion = "audit_events";

    /// <summary>
    /// Base de los eventos que legítimamente no pertenecen a ninguna cooperativa:
    /// inicios de sesión, segundo factor, invitaciones y membresías ocurren antes
    /// de elegir una.
    /// </summary>
    public const string SufijoGlobal = "Global";

    /// <summary>
    /// <b>Regla dura: para lo global se usa una constante, nunca la concatenación
    /// de un valor vacío.</b> No es estilo. Componer <c>$"{prefijo}_{tenantId}"</c>
    /// con <c>tenantId</c> vacío es exactamente lo que produjo la colección llamada
    /// literalmente <c>audit_events_</c> con más de treinta mil documentos, y una
    /// hermana <c>audit_default</c> con otros cuatrocientos por culpa de un
    /// <c>?? "default"</c>. Dos cubos de basura indistinguibles de una base real.
    /// </summary>
    public static string Para(string prefijo, string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId)
            ? $"{prefijo}_{SufijoGlobal}"
            : $"{prefijo}_{tenantId.Trim()}";
}
