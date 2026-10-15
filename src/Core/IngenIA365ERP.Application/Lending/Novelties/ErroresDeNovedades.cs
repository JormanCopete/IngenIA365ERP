using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Operations;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Los errores de las novedades de Cartera (feature 013, K3, T0878–T0883, T0888; api.md §11.1; data-model §10.1): los códigos son el contrato y los
/// mensajes, en español claro, dicen qué hacer y nunca muestran el código (FR-013, <c>TextosDeCarteraTests</c>). (nuevo)
/// </summary>
public static class ErroresDeNovedades
{
    public const string CodigoInexistente = "Lending.Novelty.NotFound";
    public const string CodigoModificacionNoAdmitida = Domain.Lending.Risk.ClasificadorDeNovedad.CodigoModificacionNoAdmitida;
    public const string CodigoParteDesconocida = "Lending.Novelty.PartyUnknown";
    public const string CodigoConsentimientoFaltante = "Lending.Novelty.ConsentMissing";
    public const string CodigoCoberturaInsuficiente = "Lending.Novelty.CoverageInsufficient";
    public const string CodigoNoAprobada = LoanNovelty.CodigoNoAprobada;
    public const string CodigoYaAplicada = LoanNovelty.CodigoYaAplicada;
    public const string CodigoCondicionesInvalidas = Domain.Lending.Schedules.GeneradorDePlan.CodigoNovedadInvalida;
    public const string CodigoEstadoInvalido = LoanNovelty.CodigoEstadoInvalido;
    public const string CodigoCreditoNoActivo = Payments.ErroresDePagos.CodigoCreditoNoActivo;
    public const string CodigoNoRechazada = "Lending.Novelty.RefusalNotResolved";
    public const string CodigoInsolvenciaInexistente = "Lending.Insolvency.NotFound";

    public static Error Inexistente() => new(CodigoInexistente, "La novedad no existe.");

    public static Error CreditoNoActivo(string numero, string estado) => new ErrorConDatos(CodigoCreditoNoActivo,
        $"El crédito {numero} no está vigente ({estado}): sólo un crédito vigente recibe novedades.", new { loanNumber = numero, status = estado });

    public static Error CondicionesInvalidas(string campo, string mensaje) => new ErrorConDatos(CodigoCondicionesInvalidas, mensaje,
        new { errors = new[] { new { field = campo, code = CodigoCondicionesInvalidas, message = mensaje } } });

    public static Error ParteDesconocida() => new(CodigoParteDesconocida,
        "Esa persona no es codeudora, avalista ni propietaria de una garantía de los créditos de la novedad: no tiene que consentir.");

    public static Error ConsentimientosFaltantes(IReadOnlyList<object> partes) => new ErrorConDatos(CodigoConsentimientoFaltante,
        "Falta el consentimiento de codeudores o garantes. Registre su aceptación firmada o decida qué hacer ante cada rechazo antes de aplicar.",
        new { parties = partes });

    public static Error CoberturaInsuficiente(decimal exigida, decimal restante) => new ErrorConDatos(CodigoCoberturaInsuficiente,
        $"Sin esa garantía la cobertura que queda ({restante:0.####}) no cumple la que exige la línea ({exigida:0.####}). No aplique la novedad o "
        + "constituya otra garantía.", new { required = exigida, remaining = restante });

    public static Error RechazoSinResolver() => new(CodigoNoRechazada,
        "Sólo se decide qué hacer ante un rechazo cuando la parte no consintió.");

    public static Error InsolvenciaInexistente() => new(CodigoInsolvenciaInexistente, "El proceso de insolvencia no existe.");

    public static Error InsolvenciaDeOtroDeudor() => new ErrorConDatos(CodigoCondicionesInvalidas,
        "El acuerdo de insolvencia es del proceso del deudor de los créditos de la novedad.", new { field = "insolvencyProceedingPublicId" });

    public static Error SoloLaRefinanciacionRecoge() => CondicionesInvalidas("loanPublicIds",
        "Sólo una refinanciación recoge varios créditos; las demás novedades son sobre un crédito.");

    public static Error CreditosDeOtroDeudor() => CondicionesInvalidas("loanPublicIds",
        "Una refinanciación recoge créditos del mismo deudor.");

    public static Error CuotasRequeridas() => CondicionesInvalidas("newTerms.installments",
        "Diga en cuántas cuotas queda el crédito (las del plan nuevo desde hoy).");

    public static Error SinMatriz(DateOnly fecha) => Common.ErroresDeGobierno.SinMatrizVigente(fecha);

    public static string Pesos(decimal valor) => ErroresDeOriginacion.Pesos(valor);
}
