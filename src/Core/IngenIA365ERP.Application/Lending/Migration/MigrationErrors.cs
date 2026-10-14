using System.Globalization;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Un error entre partes de la carga (plantillas.md §5.4): la parte, su tanda, la fila de Excel, la columna y el mensaje. (nuevo)</summary>
public sealed record ErrorEntrePartes(string Part, int? Batch, int Row, string Column, string Code, string Message);

/// <summary>
/// Los errores y avisos de la migración y la activación de Cartera (feature 013, T0745; data-model §10.1, C71, C98, C111; api.md §19). Los de la
/// forma mínima de la fase 3 siguen en <c>ErroresDeActivacion</c> (<c>AlreadyActivated</c>, <c>CutoffInvalid</c>, <c>RequirementsMissing</c>,
/// <c>MigrationLoad.NotFound</c>, <c>LoadNotDraft</c>); aquí están los que suma US8, con el mensaje en español que dice qué hacer. Los de
/// <c>api.md</c> §19 que §10.1 no lista (<c>Lending.MigrationLoad.SaleNotPending</c>, <c>Lending.Accounting.NotStarted</c>) se fijan aquí y van
/// a data-model §10.1 (C111). (nuevo)
/// </summary>
public static class ErroresDeMigracion
{
    public const string CodigoErroresEntrePartes = "Lending.Migration.CrossPartErrors";
    public const string CodigoHistorialRequerido = "Lending.Migration.ArrearsHistoryRequired";
    public const string CodigoParteFaltante = "Lending.Migration.PartMissing";
    public const string CodigoSaldoDescuadrado = "Lending.Migration.BalanceMismatch";
    public const string CodigoViviendaNoDisponible = "Lending.Migration.HousingNotAvailable";
    public const string CodigoMoraDistinta = "Lending.Migration.ArrearsMismatch";
    public const string CodigoEpisodioNoVigente = "Lending.Migration.EpisodeNotCurrent";
    public const string CodigoPlanDescuadrado = "Lending.Schedule.PrincipalMismatch";
    public const string CodigoPlazoVencido = "Lending.Activation.DeadlinePassed";
    public const string CodigoDiferenciasSinAceptar = "Lending.Activation.DifferencesNotAccepted";
    public const string CodigoCobrosExcedenLaVenta = "Lending.ProvisionalCollection.ExceedsSale";
    public const string CodigoAntesDelCorte = "Lending.Contract.BeforeCutoff";
    public const string CodigoVentaNoIncorporada = "Lending.Sale.NotIncorporated";
    public const string CodigoVentaNoPendiente = "Lending.MigrationLoad.SaleNotPending";
    public const string CodigoDiferenciaInexistente = "Lending.MigrationLoad.DifferenceNotFound";
    public const string CodigoSinContabilidad = "Lending.Accounting.NotStarted";

    private static string Pesos(decimal valor) => valor.ToString("N2", CultureInfo.GetCultureInfo("es-CO"));

    public static Error ErroresEntrePartes(IReadOnlyList<ErrorEntrePartes> errores) =>
        new ErrorConDatos(CodigoErroresEntrePartes,
            string.Create(CultureInfo.InvariantCulture,
                $"La carga tiene {errores.Count} errores entre partes. Revise el detalle por parte, fila y columna, corrija y vuelva a aplicar."),
            new
            {
                errors = errores.Select(e => new { part = e.Part, batch = e.Batch, row = e.Row, column = e.Column, code = e.Code, message = e.Message })
                    .ToList(),
            });

    public static string HistorialRequerido(int creditos) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Faltan meses del historial de mora de {creditos} créditos de segmentos cubiertos por la pérdida esperada. Cárguelos en la parte de historial.");

    public static string ParteFaltante(string parte) =>
        $"La carga no tiene filas en la parte {parte}, que es obligatoria. Cárguela antes de aplicar.";

    public static string SaldoDescuadrado(string numero, string concepto, decimal declarado, decimal calculado) =>
        $"El crédito {numero} no cuadra en {concepto}: la carga dice {Pesos(declarado)} y la suma de sus filas da {Pesos(calculado)}. "
        + "Corrija la fila o el saldo y vuelva a cargar.";

    public static string VentaDescuadrada(string factura, decimal declarado, decimal calculado) =>
        $"La venta {factura} no cuadra: el saldo al corte dice {Pesos(declarado)} y el valor original menos los cobros más las notas da "
        + $"{Pesos(calculado)}. Corrija la fila y vuelva a cargar.";

    public static string ViviendaNoDisponible(int fila) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Los créditos de vivienda todavía no se pueden migrar: estarán disponibles en una entrega posterior de Cartera. Retire la fila {fila} de la carga.");

    public static string MoraDistinta(string numero, int dias, int calculados) =>
        string.Create(CultureInfo.InvariantCulture,
            $"El crédito {numero} declara {dias} días de mora y sus cuotas dicen {calculados}. Revise la fila; si es correcta, reconozca el aviso al aplicar.");

    public static string EpisodioNoVigente(string numero, DateOnly episodio) =>
        string.Create(CultureInfo.InvariantCulture,
            $"El episodio de mora {episodio:yyyy-MM-dd} del crédito {numero} no es el vigente según sus cuotas: esa fila no se migra. Corríjala si debía migrarse.");

    public static string PlanDescuadrado(string numero, decimal plan, decimal capital) =>
        $"El plan pendiente del crédito {numero} suma {Pesos(plan)} de capital y su saldo de capital es {Pesos(capital)}. Corrija las cuotas o el saldo.";

    public static Error PlazoVencido(DateOnly fechaLimite, string? mesCerrado = null) =>
        new ErrorConDatos(CodigoPlazoVencido,
            string.Create(CultureInfo.InvariantCulture,
                $"Ya no se puede activar con esta carga: venció el plazo ({fechaLimite:yyyy-MM-dd}) o Contabilidad cerró {mesCerrado ?? "el mes"}. Aplique una carga con un corte nuevo."),
            new { deadline = fechaLimite, closedMonth = mesCerrado });

    public static Error DiferenciasSinAceptar(int pendientes) =>
        new ErrorConDatos(CodigoDiferenciasSinAceptar,
            "El cuadre o la conciliación de ventas tienen diferencias sin aceptar, o cambiaron desde que se aceptaron. Revíselas y acéptelas con motivo y acta, o corrija la carga.",
            new { pending = pendientes });

    public static Error CobrosExcedenLaVenta(string factura, decimal suma, decimal valor) =>
        new ErrorConDatos(CodigoCobrosExcedenLaVenta,
            $"Los cobros de la factura {factura} suman {Pesos(suma)}, más que el valor a crédito de la venta ({Pesos(valor)}). Corrija el archivo de cobros provisionales.",
            new { invoice = factura, sum = suma, saleAmount = valor });

    public static Error AntesDelCorte(DateOnly fechaValor, DateOnly corte) =>
        new ErrorConDatos(CodigoAntesDelCorte,
            string.Create(CultureInfo.InvariantCulture,
                $"El pago tiene fecha valor {fechaValor:yyyy-MM-dd}, igual o anterior al corte de la migración ({corte:yyyy-MM-dd}): debía venir en la carga."),
            new { valueDate = fechaValor, cutoffDate = corte });

    public static Error VentaNoIncorporada() =>
        new(CodigoVentaNoIncorporada, "Esta venta no se incorporó a Cartera: se aceptó fuera de Cartera en la activación.");

    public static Error VentaNoPendiente(Guid mensaje) =>
        new ErrorConDatos(CodigoVentaNoPendiente,
            "Esa venta no está pendiente en la conciliación de esta carga: ya está migrada o cancelada por la carga, o no es una venta hasta el corte. "
            + "Revise la lista de ventas y marque una pendiente.",
            new { messagePublicId = mensaje });

    public static Error DiferenciaInexistente(IReadOnlyList<Guid> diferencias) =>
        new ErrorConDatos(CodigoDiferenciaInexistente,
            "Alguna de las diferencias no existe en la versión vigente del cuadre de esta carga o ya estaba aceptada. Vuelva a consultar el cuadre y elija de nuevo.",
            new { differencePublicIds = diferencias });

    public static Error SinContabilidad() =>
        new(CodigoSinContabilidad,
            "La contabilidad de la cooperativa no está iniciada: no hay con qué conciliar la cartera. Iníciela en Contabilidad › Configuración.");
}
