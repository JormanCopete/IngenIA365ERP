using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.DebtorEvents;

/// <summary>
/// Los errores de los eventos del deudor, la reclamación al seguro y la insolvencia (feature 013, K3, T0885, T0888; api.md §11.4; data-model §10). Los
/// textos en español los fija <c>TextosDeCarteraTests</c>. (nuevo)
/// </summary>
public static class ErroresDeEventosDelDeudor
{
    public const string CodigoEventoDuplicado = "Lending.DebtorEvent.Duplicate";
    public const string CodigoEventoInexistente = "Lending.DebtorEvent.NotFound";
    public const string CodigoReclamacionInexistente = "Lending.InsuranceClaim.NotFound";
    public const string CodigoReclamacionNoEnTramite = "Lending.InsuranceClaim.NotInProcess";
    public const string CodigoInsolvenciaAbierta = "Lending.Insolvency.AlreadyOpen";
    public const string CodigoInsolvenciaNoNegociando = InsolvencyProceeding.CodigoNoNegociando;
    public const string CodigoInsolvenciaInexistente = Novelties.ErroresDeNovedades.CodigoInsolvenciaInexistente;

    public static Error EventoDuplicado(DebtorBlockReason clase, DateOnly desde, Guid publicId) => new ErrorConDatos(CodigoEventoDuplicado,
        $"Ese evento ya está registrado para la persona desde el {ErroresDeOriginacion.Fecha(desde)}.",
        new { kind = clase.ToString(), since = ErroresDeOriginacion.Fecha(desde), eventPublicId = publicId });

    public static Error EventoInexistente() => new(CodigoEventoInexistente, "El evento del deudor no existe.");

    public static Error ReclamacionInexistente() => new(CodigoReclamacionInexistente, "La reclamación al seguro no existe.");

    public static Error ReclamacionNoEnTramite(InsuranceClaimStatus estado) => new ErrorConDatos(CodigoReclamacionNoEnTramite,
        "La reclamación al seguro ya no está en trámite: sólo una en trámite se niega o se desiste.", new { status = estado.ToString() });

    public static Error InsolvenciaAbierta(Guid publicId, DateOnly desde) => new ErrorConDatos(CodigoInsolvenciaAbierta,
        $"La persona ya tiene un proceso de insolvencia en negociación desde el {ErroresDeOriginacion.Fecha(desde)}.",
        new { proceedingPublicId = publicId, startDate = ErroresDeOriginacion.Fecha(desde) });

    public static Error InsolvenciaNoNegociando(InsolvencyProceedingStatus estado) => new ErrorConDatos(CodigoInsolvenciaNoNegociando,
        "El proceso de insolvencia ya no está en negociación.", new { status = estado.ToString() });

    public static Error InsolvenciaInexistente() => Novelties.ErroresDeNovedades.InsolvenciaInexistente();

    /// <summary>Un dato que falta o no cuadra, con el campo (la forma de los errores de validación).</summary>
    public static Error Invalido(string campo, string mensaje) => new ErrorConDatos("Validation.Invalid", mensaje,
        new { errors = new[] { new { field = campo, code = "Invalid", message = mensaje } } });
}
