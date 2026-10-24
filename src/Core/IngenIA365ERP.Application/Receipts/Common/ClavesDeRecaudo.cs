using System.Globalization;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// La gramática de las claves de Recaudo en los contratos (feature 015, T0112; R-09; FR-006, FR-188): toda clave empieza por <see cref="Codigo"/> y dos puntos
/// (el registro de participantes la reconoce como del recaudador), la porción es <c>REC:{recibo:N}:{aplicación:N}:{clase}</c> —con <c>-{n}</c> desde el
/// segundo medio de la misma clase que cubre la misma aplicación, para que dos transferencias no compartan clave—, su reverso <c>{clave}:reverso</c>, la
/// aplicación destino de una resolución <c>{clave}:destino</c> y la línea de giro de una pagaduría <c>REC:pagaduria:{línea:N}</c>. (nuevo)
/// </summary>
public static class ClavesDeRecaudo
{
    /// <summary>El código de Recaudo como recaudador (registro de participantes, FR-147).</summary>
    public const string Codigo = "REC";

    /// <summary>Recaudo como recaudador de los contratos de Cartera y Captaciones.</summary>
    public static Recaudador Recaudador { get; } = new(Codigo, ClaseDeRecaudador.Receipts);

    /// <summary>La clave de la porción <paramref name="ordenDeLaClase"/> (1, 2…) de la clase <paramref name="clase"/> que cubre una aplicación.</summary>
    public static string Porcion(Guid recibo, Guid aplicacion, PaymentMeansClass clase, int ordenDeLaClase = 1) =>
        string.Create(CultureInfo.InvariantCulture, $"{Codigo}:{recibo:N}:{aplicacion:N}:{clase}") + (ordenDeLaClase > 1 ? $"-{ordenDeLaClase}" : string.Empty);

    /// <summary>La clave del reverso de una porción.</summary>
    public static string Reverso(string clave) => ClavesDeRecaudador.Reverso(clave);

    /// <summary>La clave de la aplicación destino de una resolución de saldo a favor.</summary>
    public static string Destino(string clave) => ClavesDeRecaudador.Destino(clave);

    /// <summary>La clave de la línea de giro de una pagaduría.</summary>
    public static string LineaDePagaduria(Guid linea) => $"{Codigo}:pagaduria:{linea:N}";

    /// <summary>¿Es una clave de Recaudo?</summary>
    public static bool EsDeRecaudo(string clave) => ClavesDeRecaudador.EsDelRecaudador(clave, Codigo);

    /// <summary>La clase de medio del contrato para una clase del catálogo (los números 1 a 9 y 99 son los mismos).</summary>
    public static ClaseDeMedioDeRecaudo ClaseDelContrato(PaymentMeansClass clase) => (ClaseDeMedioDeRecaudo)(int)clase;
}

/// <summary>
/// La huella de un recibo (feature 015, T0112; R-10; FR-172): lo que el cajero confirmó —pagador, titular, quien efectúa, fecha valor, aplicaciones con su
/// modalidad, valor y elección, medios con su valor y referencia, e imputación— sobre <see cref="HuellaDeOperacion.Calcular"/>. Excluye lo que cambia sin que
/// cambie la intención: la simulación, el aprobador y el instante. (nuevo)
/// </summary>
public static class HuellaDeRecibo
{
    public const string Operacion = "Recaudo.Recibo";

    /// <summary>La operación de la huella de un recibo tal como quedó guardado (feature 015, T0194; TR37; nuevo).</summary>
    public const string OperacionDelRecibo = "Recaudo.Recibo.Guardado";

    public static string Calcular(object insumos) => HuellaDeOperacion.Calcular(Operacion, insumos);

    /// <summary>
    /// La huella de un recibo <b>recalculada desde lo guardado</b> (feature 015, US6, T0189a, T0194; TR37; nuevo): número, canal, pagador, fecha valor, totales y, por
    /// aplicación, destino, producto, titular y valor, y por porción su clave y valor. La lleva el evento <c>Receipts.ProcessingRun.Summary</c>; recalculada después, una
    /// alteración del recibo no coincide.
    /// </summary>
    public static async Task<string> DelReciboAsync(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext db,
        IngenIA365ERP.Domain.Entities.Receipts.Operations.Receipt recibo, CancellationToken ct)
    {
        var aplicaciones = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.ReceiptApplications.Where(a => a.ReceiptId == recibo.Id).OrderBy(a => a.Ordinal)
                .Select(a => new { a.Id, a.Ordinal, a.Target, a.ProductPublicId, a.BeneficiaryPersonId, a.MiscConceptId, a.Amount }), ct);
        var ids = aplicaciones.Select(a => a.Id).ToList();
        var porciones = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.ReceiptApplicationPortions.Where(p => ids.Contains(p.ReceiptApplicationId)).OrderBy(p => p.IdempotencyKey)
                .Select(p => new { p.IdempotencyKey, p.Amount }), ct);
        return HuellaDeOperacion.Calcular(OperacionDelRecibo, new
        {
            recibo.PublicId, recibo.Number, recibo.Channel, recibo.PayerPersonId, recibo.ValueDate, recibo.TotalApplied, recibo.TotalMeans, recibo.OperationKey,
            Aplicaciones = aplicaciones.Select(a => new { a.Ordinal, a.Target, a.ProductPublicId, a.BeneficiaryPersonId, a.MiscConceptId, a.Amount }),
            Porciones = porciones,
        });
    }
}
