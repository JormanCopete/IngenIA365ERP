using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Application.Lending.WriteOffs;

/// <summary>
/// Los códigos y mensajes del castigo (feature 013, K3, US14, T0914; data-model §10.1): el lote inexistente, el crédito que no cumple (C98), el que ya
/// está en el lote o en otro lote abierto, el que no está vigente, el ítem inexistente, el consejo que falta y el soporte que no corresponde. Los de las
/// transiciones del lote los dice <see cref="WriteOffBatch"/>. (nuevo)
/// </summary>
public static class ErroresDeCastigo
{
    public const string CodigoInexistente = "Lending.WriteOffBatch.NotFound";
    public const string CodigoNoBorrador = WriteOffBatch.CodigoNoBorrador;
    public const string CodigoAprobacionRequerida = WriteOffBatch.CodigoAprobacionRequerida;
    public const string CodigoEstadoInvalido = WriteOffBatch.CodigoEstadoInvalido;
    public const string CodigoRequisitoFaltante = RequisitosDeCastigo.CodigoRequisitoFaltante;
    public const string CodigoYaIncluido = "Lending.WriteOffBatch.LoanAlreadyIncluded";
    public const string CodigoEnOtroLote = "Lending.WriteOff.LoanInAnotherBatch";
    public const string CodigoItemInexistente = "Lending.WriteOffBatch.ItemNotFound";
    public const string CodigoCreditoNoActivo = Payments.ErroresDePagos.CodigoCreditoNoActivo;
    public const string CodigoSinConsejo = "Lending.WriteOff.BoardMissing";
    public const string CodigoSoporteInvalido = "Lending.WriteOff.AttachmentInvalid";
    public const string CodigoNingunoCumple = "Lending.WriteOff.NoLoansLeft";

    public static Error Inexistente() => new(CodigoInexistente, "El lote de castigo no existe.");

    public static Error ItemInexistente() => new(CodigoItemInexistente, "Ese crédito no está en el lote de castigo.");

    /// <summary>C98: el crédito no cumple lo que la norma o la política exigen para castigarlo; <c>data.missing[]</c> con las claves que faltan.</summary>
    public static Error RequisitoFaltante(string numero, VerificacionDeCastigo v) => new ErrorConDatos(CodigoRequisitoFaltante,
        $"El crédito {numero} no se puede castigar: falta {string.Join(", ", v.Faltantes.Select(Requisito))}. Cúmplalo o retire el crédito del lote.",
        new { loanNumber = numero, missing = v.Faltantes });

    /// <summary>Lo que falta, en palabras de la persona.</summary>
    public static string Requisito(string clave) => clave switch
    {
        RequisitosDeCastigo.NoEsAsociado => "que el deudor ya no sea asociado",
        RequisitosDeCastigo.AportesCruzados => "el cruce de los aportes",
        RequisitosDeCastigo.InformeMotivado => "el informe motivado",
        RequisitosDeCastigo.ConceptoDeIrrecuperabilidad => "el concepto de irrecuperabilidad",
        RequisitosDeCastigo.DeterioroTotal => "el deterioro total del saldo",
        RequisitosDeCastigo.AlturaMinima => "la altura de mora mínima",
        RequisitosDeCastigo.GestionesAgotadas => "agotar las gestiones de cobro",
        _ => clave,
    };

    /// <summary>
    /// Bloque D: al aplicar, ningún crédito del lote cumple ya los requisitos; el lote no se aplica. <c>data.excluded[]</c> con cada crédito, lo que le
    /// falta y el motivo. Anule el lote o espere a que vuelvan a cumplir.
    /// </summary>
    public static Error NingunoCumple(string lote, IReadOnlyList<WriteOffExclusionDto> excluidos) => new ErrorConDatos(CodigoNingunoCumple,
        $"Ningún crédito del lote {lote} cumple ya los requisitos del castigo: el lote no se aplica. Revise lo que falta en cada uno o anule el lote.",
        new
        {
            batchNumber = lote,
            excluded = excluidos.Select(e => new { loanPublicId = e.LoanPublicId, loanNumber = e.LoanNumber, missing = e.Missing, reason = e.Reason }).ToList(),
        });

    public static Error YaIncluido(string numero) => new ErrorConDatos(CodigoYaIncluido,
        $"El crédito {numero} ya está en este lote de castigo.", new { loanNumber = numero });

    public static Error EnOtroLote(string numero, string lote) => new ErrorConDatos(CodigoEnOtroLote,
        $"El crédito {numero} ya está en el lote de castigo {lote}, que todavía no se aplicó ni se anuló.", new { loanNumber = numero, batchNumber = lote });

    public static Error CreditoNoActivo(string numero, string estado) => new ErrorConDatos(CodigoCreditoNoActivo,
        $"El crédito {numero} no está vigente ({estado}): sólo un crédito vigente se castiga.", new { loanNumber = numero, status = estado });

    public static Error SinConsejo() => new(CodigoSinConsejo,
        "La cooperativa no tiene un consejo de administración activo entre sus órganos: el castigo lo aprueba el consejo con acta. Regístrelo en Órganos.");

    public static Error SoporteInvalido() => new(CodigoSoporteInvalido,
        "El soporte del reporte al supervisor no es un adjunto disponible de este lote. Súbalo como soporte del reporte del lote y vuelva a intentarlo.");
}
