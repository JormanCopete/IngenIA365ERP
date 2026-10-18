using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>
/// Los códigos y mensajes de la cobranza de K4 (feature 013, US12, T0954; api.md §14.3–§14.9; data-model §10.1): etapas, reglas de reparto,
/// asignaciones, gestiones, promesas, acuerdos, cobro jurídico, gastos, campañas, avisos previos y reclamos sobre el reporte. Los <c>*.NotFound</c>
/// son 404 —también fuera del alcance, con el mismo mensaje—; los demás, 422 con <c>data</c> para corregir. Los mensajes van en español claro,
/// dicen qué hacer y nunca muestran el código (FR-013). Los que rechaza una entidad o un motor de cobranza llegan como <c>ProblemaDeCartera</c> y se
/// traducen con <c>ErroresDeGobierno.De</c>. Los de los mensajes al deudor son de <see cref="ErroresDeMensajesAlDeudor"/>. (nuevo)
/// </summary>
public static class ErroresDeCobranza
{
    // --------------------------------------------------------------------------------------------------------- etapas --
    public const string CodigoRangosInvalidos = EtapasDeCobranza.CodigoRangosInvalidos;
    public const string CodigoVigenciaDeEtapaInvalida = CollectionStage.CodigoVigenciaInvalida;
    public const string CodigoReglaDeGastoInvalida = ReglaDeGastoDeCobranza.CodigoReglaInvalida;

    // ---------------------------------------------------------------------------------------- reparto y asignaciones --
    public const string CodigoReglaDeRepartoInvalida = RepartoDeGestores.CodigoReglaInvalida;
    public const string CodigoVigenciaDeReglaInvalida = CollectorAssignmentRule.CodigoVigenciaInvalida;
    public const string CodigoReglaInexistente = "Lending.CollectorAssignmentRule.NotFound";
    public const string CodigoGestorInexistente = "Lending.Collections.CollectorNotFound";
    public const string CodigoSinEtapa = "Lending.Collections.LoanNotInCollection";

    // ------------------------------------------------------------------------------------------- gestiones y promesas --
    public const string CodigoContactoNoObligado = "Lending.CollectionAction.ContactNotObligated";
    public const string CodigoGestionInexistente = "Lending.CollectionAction.NotFound";
    public const string CodigoGestionYaAnulada = "Lending.CollectionAction.AlreadyVoided";
    public const string CodigoGestionDeSistema = "Lending.CollectionAction.SystemRecord";

    // -------------------------------------------------------------------------------------------------------- acuerdos --
    public const string CodigoAcuerdoInexistente = "Lending.PaymentAgreement.NotFound";
    public const string CodigoCambiaCondiciones = "Lending.PaymentAgreement.ChangesTerms";
    public const string CodigoAcuerdoNoPropuesto = PaymentAgreement.CodigoNoPropuesto;
    public const string CodigoEstadoDeAcuerdoInvalido = PaymentAgreement.CodigoEstadoInvalido;
    public const string CodigoCalendarioInvalido = PaymentAgreement.CodigoCalendarioInvalido;
    public const string CodigoAcuerdoAbierto = "Lending.PaymentAgreement.AlreadyOpen";
    public const string CodigoSegregacion = PaymentAgreement.CodigoSegregacion;

    // ------------------------------------------------------------------------------------------------- cobro jurídico --
    public const string CodigoProcesoInexistente = "Lending.LegalProcess.NotFound";
    public const string CodigoFueraDeEtapaJuridica = "Lending.LegalProcess.LoanNotInLegalStage";
    public const string CodigoEstadoDeProcesoInvalido = LegalProcess.CodigoEstadoInvalido;
    public const string CodigoProcesoAbierto = "Lending.LegalProcess.AlreadyOpen";
    public const string CodigoSoporteJuridicoInvalido = "Lending.LegalProcess.AttachmentInvalid";
    public const string CodigoAbogadoEsElDemandado = "Lending.LegalProcess.LawyerIsDebtor";
    public const string CodigoInsolvenciaEnNegociacion = "Lending.LegalProcess.InsolvencyNegotiating";

    // ---------------------------------------------------------------------------------------------- gastos de cobranza --
    public const string CodigoGastoInexistente = "Lending.CollectionFee.NotFound";
    public const string CodigoAvisoDeGastoSinEnviar = "Lending.CollectionFee.NoticeNotSent";
    public const string CodigoGastoYaCausado = "Lending.CollectionFee.AlreadyCaused";
    public const string CodigoGastoSobreElTope = "Lending.CollectionFee.AboveCap";
    public const string CodigoTarifaSobreElTope = TopeDelGastoDeCobranza.CodigoPorEncimaDelTope;

    // -------------------------------------------------------------------------------------------------------- campañas --
    public const string CodigoCampanaInexistente = "Lending.Campaign.NotFound";
    public const string CodigoEstadoDeCampanaInvalido = MessageCampaign.CodigoEstadoInvalido;
    public const string CodigoCampanaDuplicada = "Lending.Campaign.CodeDuplicate";
    public const string CodigoCriteriosInvalidos = "Lending.Campaign.FiltersInvalid";

    // --------------------------------------------------------------------------------------------------- avisos previos --
    public const string CodigoAvisoInexistente = "Lending.PriorNotice.NotFound";
    public const string CodigoAvisoCompleto = "Lending.PriorNotice.AlreadyEvidenced";
    public const string CodigoConstanciaRepetida = "Lending.PriorNotice.EvidenceDuplicate";
    public const string CodigoSoporteDeAvisoInvalido = "Lending.PriorNotice.AttachmentInvalid";
    public const string CodigoEpisodioCerrado = "Lending.PriorNotice.EpisodeClosed";

    // ---------------------------------------------------------------------------------------- reclamos sobre el reporte --
    public const string CodigoReclamoInexistente = "Lending.CreditReportClaim.NotFound";
    public const string CodigoReclamoNoEnTramite = "Lending.CreditReportClaim.NotInProcess";
    public const string CodigoReclamoYaProrrogado = "Lending.CreditReportClaim.AlreadyExtended";
    public const string CodigoSoporteDeReclamoInvalido = "Lending.CreditReportClaim.AttachmentInvalid";

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static string Pesos(decimal valor) => valor.ToString("C0", Colombia);

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // --------------------------------------------------------------------------------------------------------- etapas --

    public static Error SinEtapa(string credito) => new ErrorConDatos(CodigoSinEtapa,
        $"El crédito {credito} no está en cobranza: no tiene etapa vigente (nunca entró o salió al ponerse al día). Se gestiona cuando el proceso diario lo "
        + "haga entrar a una etapa.", new { loanNumber = credito });

    // ---------------------------------------------------------------------------------------- reparto y asignaciones --

    public static Error ReglaInexistente() => new(CodigoReglaInexistente, "La regla de reparto no existe.");

    public static Error GestorInexistente() => new(CodigoGestorInexistente,
        "El gestor no es un usuario activo de esta cooperativa. Elíjalo de la lista de usuarios.");

    // ------------------------------------------------------------------------------------------- gestiones y promesas --

    public static Error ContactoNoObligado(string credito) => new ErrorConDatos(CodigoContactoNoObligado,
        $"La gestión sólo se hace con el deudor o un codeudor obligado del crédito {credito}, o ante su pagaduría. Nunca con una referencia.",
        new { loanNumber = credito });

    public static Error GestionInexistente() => new(CodigoGestionInexistente, "La gestión de cobranza no existe.");

    public static Error GestionYaAnulada() => new(CodigoGestionYaAnulada,
        "La gestión ya fue anulada por otra: corrija la gestión que la anuló.");

    public static Error GestionDeSistema() => new(CodigoGestionDeSistema,
        "Esa gestión la dejó el proceso diario (la salida de cobranza): es la constancia de lo que pasó y no se anula.");

    // -------------------------------------------------------------------------------------------------------- acuerdos --

    public static Error AcuerdoInexistente() => new(CodigoAcuerdoInexistente, "El acuerdo de pago no existe.");

    public static Error CambiaCondiciones(decimal acordado, decimal vencido) => new ErrorConDatos(CodigoCambiaCondiciones,
        vencido <= 0m
            ? "El crédito no tiene nada vencido: un acuerdo de pago es sólo sobre lo vencido. Para cambiar las condiciones, tramite una novedad."
            : $"El acuerdo suma {Pesos(acordado)} y lo vencido es {Pesos(vencido)}: un acuerdo de pago es sólo sobre lo vencido, sin cambiar las "
              + "condiciones del crédito. Para cambiar plazo, cuota o tasa, tramite una novedad.",
        new { agreed = acordado, pastDue = vencido });

    public static Error AcuerdoAbierto(string numero) => new ErrorConDatos(CodigoAcuerdoAbierto,
        $"El crédito ya tiene el acuerdo de pago {numero} propuesto o vigente. Anúlelo antes de proponer otro.", new { number = numero });

    // ------------------------------------------------------------------------------------------------- cobro jurídico --

    public static Error ProcesoInexistente() => new(CodigoProcesoInexistente, "El proceso de cobro jurídico no existe.");

    public static Error FueraDeEtapaJuridica(string credito, string? etapa) => new ErrorConDatos(CodigoFueraDeEtapaJuridica,
        etapa is null
            ? $"El crédito {credito} no está en una etapa de cobranza: sólo un crédito en la etapa jurídica se entrega a un abogado."
            : $"El crédito {credito} está en la etapa {etapa}: sólo un crédito en la etapa jurídica se entrega a un abogado.",
        new { loanNumber = credito, stage = etapa });

    public static Error ProcesoAbierto(string credito, string numero) => new ErrorConDatos(CodigoProcesoAbierto,
        $"El crédito {credito} ya está en el proceso de cobro jurídico {numero}, que sigue abierto.", new { loanNumber = credito, number = numero });

    public static Error AbogadoEsElDemandado() => new(CodigoAbogadoEsElDemandado,
        "El abogado no puede ser el deudor demandado. Elija como abogado a otra persona del maestro.");

    public static Error InsolvenciaEnNegociacion(string proceso, Guid insolvencia) => new ErrorConDatos(CodigoInsolvenciaEnNegociacion,
        $"El proceso {proceso} sigue suspendido: el deudor está en una negociación de insolvencia. Se reanuda cuando la insolvencia termine "
        + "(con acuerdo o con su fracaso registrado).", new { number = proceso, insolvencyPublicId = insolvencia });

    public static Error SoporteJuridicoInvalido() => new(CodigoSoporteJuridicoInvalido,
        "El soporte no es un documento disponible de este proceso. Súbalo como documento del proceso y vuelva a intentarlo.");

    // ---------------------------------------------------------------------------------------------- gastos de cobranza --

    public static Error GastoInexistente() => new(CodigoGastoInexistente, "El gasto de cobranza no existe o no es de este deudor.");

    public static Error AvisoDeGastoSinEnviar(string credito) => new ErrorConDatos(CodigoAvisoDeGastoSinEnviar,
        $"El gasto de cobranza del crédito {credito} todavía no se le informó al deudor: se causa sólo después de que el aviso salga. "
        + "Espere a que el aviso quede enviado.", new { loanNumber = credito });

    public static Error GastoYaCausado(string credito) => new ErrorConDatos(CodigoGastoYaCausado,
        $"El gasto de cobranza del crédito {credito} ya se causó.", new { loanNumber = credito });

    public static Error GastoSobreElTope(string credito, decimal valor, decimal maximo) => new ErrorConDatos(CodigoGastoSobreElTope,
        $"Lo que se cobraría al deudor del crédito {credito} ({Pesos(valor)}) supera el tope del gasto de cobranza de la cooperativa ({Pesos(maximo)}). "
        + "Registre a cargo del deudor a lo sumo ese valor, o el resto sin cobrárselo.", new { loanNumber = credito, amount = valor, maxAmount = maximo });

    // -------------------------------------------------------------------------------------------------------- campañas --

    public static Error CampanaInexistente() => new(CodigoCampanaInexistente, "La campaña no existe.");

    public static Error CampanaDuplicada(string codigo) => new ErrorConDatos(CodigoCampanaDuplicada,
        $"Ya hay una campaña con el código {codigo}. Use otro código.", new { code = codigo });

    public static Error CriteriosInvalidos(string motivo) => new ErrorConDatos(CodigoCriteriosInvalidos, motivo, new { reason = motivo });

    // --------------------------------------------------------------------------------------------------- avisos previos --

    public static Error AvisoInexistente() => new(CodigoAvisoInexistente, "El aviso previo no existe.");

    public static Error AvisoCompleto(string credito) => new ErrorConDatos(CodigoAvisoCompleto,
        $"El aviso previo del crédito {credito} ya tiene las constancias que exige este episodio: no hace falta otra comunicación.",
        new { loanNumber = credito });

    public static Error ConstanciaRepetida(int comunicacion) => new ErrorConDatos(CodigoConstanciaRepetida,
        $"La comunicación {comunicacion} de este aviso ya tiene su constancia.", new { communicationNumber = comunicacion });

    public static Error SoporteDeAvisoInvalido() => new(CodigoSoporteDeAvisoInvalido,
        "La guía de la carta no es un documento disponible de este aviso. Súbala como constancia del aviso y vuelva a intentarlo.");

    public static Error EpisodioCerrado(string credito) => new ErrorConDatos(CodigoEpisodioCerrado,
        $"El crédito {credito} se puso al día y el episodio de este aviso terminó: si vuelve a caer en mora, se abre un aviso nuevo.",
        new { loanNumber = credito });

    // ---------------------------------------------------------------------------------------- reclamos sobre el reporte --

    public static Error ReclamoInexistente() => new(CodigoReclamoInexistente, "El reclamo sobre el reporte no existe.");

    public static Error ReclamoNoEnTramite(string estado) => new ErrorConDatos(CodigoReclamoNoEnTramite,
        $"El reclamo ya no está en trámite ({estado}): sólo un reclamo en trámite se prorroga, se responde o se retira.", new { status = estado });

    public static Error ReclamoYaProrrogado(DateOnly vence) => new ErrorConDatos(CodigoReclamoYaProrrogado,
        $"El reclamo ya se prorrogó (vence el {Fecha(vence)}): la prórroga se concede una sola vez.", new { extendedDueDate = vence });

    public static Error SoporteDeReclamoInvalido() => new(CodigoSoporteDeReclamoInvalido,
        "El soporte no es un documento disponible del reclamo de esta persona. Súbalo como soporte del reclamo y vuelva a intentarlo.");
}
