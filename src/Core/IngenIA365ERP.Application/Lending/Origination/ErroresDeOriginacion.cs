using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// Los errores del simulador y de la originación de Cartera (feature 013, US2; api.md §1.2, §4.1, §4.2; data-model §2, §9, §10.1).
/// <c>Lending.Application.NotFound</c> es 404 —también sin permiso o fuera del alcance de oficina o línea, con el mismo mensaje—; los
/// demás, 422 con <c>data</c> para corregir. Los mensajes van en español claro, dicen qué hacer y nunca muestran el código (FR-013;
/// <c>TextosDeCarteraTests</c>). (nuevo)
/// </summary>
public static class ErroresDeOriginacion
{
    public const string CodigoSolicitudInexistente = "Lending.Application.NotFound";
    public const string CodigoNoAsociado = "Lending.Application.NotAssociate";
    public const string CodigoParteRepetida = CreditApplicationParty.CodigoParteRepetida;
    public const string CodigoNoEditable = CreditApplication.CodigoNoEditable;
    public const string CodigoEstadoInvalido = CreditApplication.CodigoEstadoInvalido;
    public const string CodigoRequisitoDesconocido = "Lending.Application.RequirementUnknown";
    public const string CodigoDocumentosFaltantes = "Lending.Application.DocumentsMissing";
    public const string CodigoNoEstaEnAnalisis = "Lending.Application.NotInAnalysis";
    public const string CodigoYaDesembolsada = "Lending.Application.AlreadyDisbursed";
    public const string CodigoFormaDePagoNoAdmitida = "Lending.Application.PaymentFormNotAllowed";
    public const string CodigoRequisitosDeLinea = "Lending.Application.RequirementsNotMet";
    public const string CodigoLibranzaNoDisponible = "Lending.PayrollAgreement.NotAvailable";
    public const string CodigoLineaCerrada = "Lending.CreditLine.Closed";
    public const string CodigoFueraDeRango = "Lending.Schedule.OutOfLineRange";
    public const string CodigoUsuraExcedida = ControlDeUsura.CodigoExcedida;
    public const string CodigoSinAutorizacion = "Lending.Authorization.Missing";
    public const string CodigoSinEvidencia = "Lending.Authorization.WithoutEvidence";
    public const string CodigoCapacidadDePago = "Lending.Capacity.PaymentInsufficient";
    public const string CodigoCapacidadDeDescuento = "Lending.Capacity.DeductionInsufficient";
    public const string CodigoCupoInsuficiente = "Lending.CreditLimit.Insufficient";
    public const string CodigoConcentracionExcedida = "Lending.Concentration.LimitExceeded";
    public const string CodigoSinPatrimonioTecnico = "Lending.Concentration.TechnicalCapitalUnavailable";
    public const string CodigoCodeudoresInsuficientes = "Lending.Collateral.CoDebtorsInsufficient";
    public const string CodigoDeudorBloqueado = DebtorBlock.CodigoBloqueado;
    public const string CodigoCoincidenciaEnListas = "Compliance.RestrictiveList.Match";
    public const string CodigoConsultaACentralesRequerida = "Lending.Bureau.QueryRequired";
    public const string CodigoSimuladoEnProduccion = "Lending.Adapter.SimulatedInProduction";
    public const string CodigoCanalNoDisponible = "Lending.Adapter.Unavailable";
    public const string CodigoSinCorreo = "Lending.DebtorMessage.DestinationMissing";
    public const string CodigoConcurrencia = "Concurrency.StaleRowVersion";

    /// <summary>La entrega desde la que existe la libranza de pagaduría externa (FR-109).</summary>
    public const string EntregaDeLibranza = "K2";

    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    public static Error SolicitudInexistente() => new(CodigoSolicitudInexistente, "La solicitud de crédito no existe.");

    public static Error NoAsociado(Guid persona, string nombre) => new ErrorConDatos(CodigoNoAsociado,
        $"{nombre} no es asociado activo y la línea (o el perfil regulatorio de la cooperativa) no admite créditos a no asociados. " +
        "Afílielo primero o elija una línea que admita no asociados.",
        new { personPublicId = persona });

    public static Error ParteRepetida(Guid persona) => new ErrorConDatos(CodigoParteRepetida,
        "Una persona aparece dos veces en la solicitud (como solicitante y codeudor, o como codeudor repetido). Quítela de una de las dos.",
        new { personPublicId = persona });

    public static Error NoEditable(string estado) => new ErrorConDatos(CodigoNoEditable,
        "La solicitud ya no se puede modificar: sólo se edita en borrador o cuando se devolvió para corregir.", new { status = estado });

    public static Error EstadoInvalido(string estado, string pedido) => new ErrorConDatos(CodigoEstadoInvalido,
        $"La solicitud está en «{estado}» y no admite esa acción.", new { status = estado, requested = pedido });

    public static Error RequisitoDesconocido(string codigo) => new ErrorConDatos(CodigoRequisitoDesconocido,
        $"«{codigo}» no es un documento que pida la línea de esta solicitud. Elija uno de la lista de documentos de la línea.",
        new { requirementCode = codigo });

    public static Error DocumentosFaltantes(IReadOnlyList<DocumentoFaltante> faltan) => new ErrorConDatos(CodigoDocumentosFaltantes,
        $"Faltan documentos obligatorios para enviar la solicitud a análisis: {string.Join(", ", faltan.Select(f => f.Name))}. Adjúntelos y vuelva a enviarla.",
        new { missing = faltan.Select(f => new { code = f.Code, name = f.Name, personPublicId = f.PersonPublicId }).ToList() });

    public static Error NoEstaEnAnalisis(string estado) => new ErrorConDatos(CodigoNoEstaEnAnalisis,
        "El análisis se registra mientras la solicitud espera decisión.", new { status = estado });

    public static Error YaDesembolsada() => new(CodigoYaDesembolsada,
        "La solicitud ya generó un crédito o un cupo: no se anula. Si hace falta, anule el crédito pendiente de giro.");

    public static Error FormaDePagoNoAdmitida(string forma, string motivo) => new ErrorConDatos(CodigoFormaDePagoNoAdmitida,
        motivo == "NotEmployee"
            ? "La forma de pago por nómina propia exige que el solicitante sea empleado de la cooperativa en el maestro de personas."
            : "La línea no admite esa forma de pago. Elija una de las que ofrece la línea.",
        new { paymentForm = forma, reason = motivo });

    public static Error LibranzaNoDisponible() => new ErrorConDatos(CodigoLibranzaNoDisponible,
        "La libranza con pagadurías externas todavía no está disponible: llega con una entrega posterior de Cartera. Use la nómina propia o la caja.",
        new { availableFrom = EntregaDeLibranza });

    public static Error LineaCerrada(string codigo, string motivo) => new ErrorConDatos(CodigoLineaCerrada,
        $"La línea {codigo} está cerrada a créditos nuevos ({motivo}).", new { creditLineCode = codigo, reason = motivo });

    public static Error LineaBloqueadaPorUsura(string codigo, DateOnly desde) => new ErrorConDatos(CodigoUsuraExcedida,
        $"La línea {codigo} está cerrada a créditos nuevos desde el {Fecha(desde)}: su tasa supera la usura certificada. Registre una versión bajo la usura.",
        new { creditLineCode = codigo, usuryBlockedSince = Fecha(desde) });

    /// <summary>Monto, plazo, periodicidad, sistema, gracia o extraordinarias fuera de lo que admite la línea (api.md §4.1).</summary>
    public static Error FueraDeRango(string campo, object? minimo, object? maximo, string mensaje) => new ErrorConDatos(CodigoFueraDeRango, mensaje,
        new { field = campo, min = minimo, max = maximo, errors = new[] { new ErrorDeCampo(campo, CodigoFueraDeRango, mensaje) } });

    public static Error SinAutorizacion(Guid persona, string nombre, DataAuthorizationPurpose finalidad) => new ErrorConDatos(CodigoSinAutorizacion,
        $"Falta la autorización de {nombre} para {Finalidad(finalidad)}. Regístrela con su evidencia.",
        new
        {
            personPublicId = persona,
            purpose = finalidad.ToString(),
            howToFix = "Registre la autorización en la solicitud (con su evidencia firmada) o en Cumplimiento › Habeas data.",
        });

    public static Error SinCorreo(Guid persona, string nombre) => new ErrorConDatos(CodigoSinCorreo,
        $"{nombre} no tiene correo registrado en el maestro de personas. Regístrelo para enviarle la simulación.", new { personPublicId = persona });

    /// <summary>
    /// La concentración excedida (FR-041, SC-022): por deudor o por grupo conectado, con el límite, lo actual, lo pedido, el exceso, la base
    /// (el patrimonio técnico y su corte) y, en el grupo, los miembros que suman.
    /// </summary>
    public static Error ConcentracionExcedida(MedicionDeConcentracion m) => new ErrorConDatos(CodigoConcentracionExcedida,
        m.Alcance == AlcanceDeConcentracion.Group
            ? $"La operación llevaría el endeudamiento del grupo conectado a {Pesos(m.Actual + m.Pedido)}, por encima del límite de {Pesos(m.Limite)} " +
              $"({Pesos(m.Exceso)} de exceso)."
            : $"La operación llevaría el endeudamiento de la persona a {Pesos(m.Actual + m.Pedido)}, por encima del límite de {Pesos(m.Limite)} " +
              $"({Pesos(m.Exceso)} de exceso).",
        new
        {
            scope = m.Alcance.ToString(),
            groupPublicId = m.GrupoPublicId,
            limit = m.Limite,
            current = m.Actual,
            requested = m.Pedido,
            excess = m.Exceso,
            @base = m.Base,
            fraction = m.Fraccion,
            cutoffDate = m.Corte is { } c ? Fecha(c) : null,
            withAdmissibleCollateral = m.ConGarantiaAdmisible,
            members = m.Miembros.Select(x => new { personPublicId = x.PersonPublicId, exposure = x.Exposicion }).ToList(),
        });

    public static Error SinPatrimonioTecnico(DateOnly fecha) => new ErrorConDatos(CodigoSinPatrimonioTecnico,
        "No hay patrimonio técnico vigente registrado en Contabilidad: no se puede validar el límite de concentración. Pida al contador registrarlo.",
        new { date = Fecha(fecha) });

    public static Error ConcurrenciaVieja() => Error.StaleRowVersion;

    public static string Finalidad(DataAuthorizationPurpose finalidad) => finalidad switch
    {
        DataAuthorizationPurpose.GeneralTreatment => "el tratamiento de sus datos",
        DataAuthorizationPurpose.CreditProcessing => "el estudio de su crédito",
        DataAuthorizationPurpose.CreditBureauQuery => "la consulta a centrales de riesgo",
        _ => finalidad.ToString(),
    };

    public static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Pesos(decimal valor) => "$ " + valor.ToString("#,0.##", EsCo);
}

/// <summary>Un documento obligatorio que falta (api.md §4.2, <c>data.missing[]</c>). (nuevo)</summary>
public sealed record DocumentoFaltante(string Code, string Name, Guid? PersonPublicId = null);
