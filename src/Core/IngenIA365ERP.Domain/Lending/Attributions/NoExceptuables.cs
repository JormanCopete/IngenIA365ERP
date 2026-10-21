namespace IngenIA365ERP.Domain.Lending.Attributions;

/// <summary>
/// Lo que ningún nivel de la matriz puede exceptuar (R132, T63): la falta de capacidad de pago, la falta de autorización del titular,
/// la coincidencia en listas restrictivas y la usura. Viajan en <c>data.reasons[]</c> de <c>Lending.Attribution.NotExceptionable</c>
/// y en <c>nonExceptionable[]</c> de la vista previa. (nuevo)
/// </summary>
public enum MotivoNoExceptuable
{
    PaymentCapacity = 1,
    HolderAuthorization = 2,
    RestrictiveListMatch = 3,
    Usury = 4,
}

/// <summary>
/// Motor puro de lo no exceptuable (feature 013, T0277; R132): recibe los hallazgos del sujeto (los calculan la capacidad de pago, las
/// autorizaciones, las listas y la usura) y devuelve los motivos que impiden decidir, sin repetir y en el orden fijo del enum. Ninguna
/// excepción, ninguna regla y ningún nivel los levanta. (nuevo)
/// </summary>
public static class NoExceptuables
{
    public const string CodigoDeError = "Lending.Attribution.NotExceptionable";

    public static IReadOnlyList<MotivoNoExceptuable> Evaluar(IEnumerable<MotivoNoExceptuable> hallazgos)
    {
        ArgumentNullException.ThrowIfNull(hallazgos);
        return hallazgos
            .Select(h => Enum.IsDefined(h) ? h : throw new ArgumentException($"{(int)h} no es un motivo no exceptuable.", nameof(hallazgos)))
            .Distinct()
            .Order()
            .ToList();
    }
}
