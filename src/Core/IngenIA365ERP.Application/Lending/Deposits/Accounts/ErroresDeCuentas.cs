using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>
/// Los errores de las cuentas de captación y de aportes (feature 014, US2, T073–T075; contracts/api.md §1.8, §4, §10). <c>Lending.Savings.NotFound</c>
/// es 404 —también sin permiso o fuera del alcance, con el mismo mensaje—; los demás, 422 con <c>data</c>. Los de titularidad los arma el dominio
/// (<c>ReglasDeTitularidad</c>) y llegan por <see cref="De"/>. Mensajes en español claro que dicen qué hacer (FR-013). (nuevo)
/// </summary>
public static class ErroresDeCuentas
{
    public const string CodigoCuentaInexistente = "Lending.Savings.NotFound";
    public const string CodigoMarcaInexistente = "Lending.Gmf.NotFound";
    public const string CodigoCaptacionesSinActivar = "Lending.Savings.NotActivated";
    public const string CodigoAportesSinActivar = "Lending.Contributions.NotActivated";
    public const string CodigoSoloAsociados = "Lending.Savings.MembersOnly";
    public const string CodigoAportesYaAbierta = "Lending.Contributions.AccountAlreadyOpen";
    public const string CodigoPermanenteYaAbierto = "Lending.Savings.PermanentAlreadyOpen";
    public const string CodigoLineaNoAbre = "Lending.Savings.LineNotOpen";
    public const string CodigoLineaSinVersion = "Lending.Savings.LineWithoutVersion";
    public const string CodigoUsarRutaDeCdat = "Lending.Savings.UseTermDepositRoute";
    public const string CodigoOficinaNoAdmitida = "Lending.Savings.OfficeNotAllowedByLine";
    public const string CodigoTitularFallecido = "Lending.Savings.HolderDeceased";
    public const string CodigoDatosIncompletos = "Lending.Savings.MemberDataIncomplete";
    public const string CodigoOrigenDeFondos = "Lending.Savings.SourceOfFundsRequired";
    public const string CodigoFechaDeApertura = "Lending.Savings.OpeningDateInvalid";
    public const string CodigoNoEnApertura = "Lending.Savings.NotOpening";
    public const string CodigoListasBloquean = "Lending.Screening.Blocked";
    public const string CodigoSinActualizacionPendiente = "Lending.Savings.DataUpdateNotRequired";
    public const string CodigoCuentaCerrada = Domain.Entities.Lending.Deposits.Accounts.MemberAccount.CodigoCerrada;
    public const string CodigoPoliticaSinMarca = "Lending.Gmf.PolicyWithoutMark";
    public const string CodigoMarcaRetroactiva = "Lending.Gmf.Retroactive";
    public const string CodigoMarcaTerminada = "Lending.Gmf.AlreadyEnded";

    public static Error CuentaInexistente() => new(CodigoCuentaInexistente, "La cuenta no existe.");

    public static Error MarcaInexistente() => new(CodigoMarcaInexistente, "La marca de exención del GMF no existe.");

    /// <summary>Lo que rechazó una regla de dominio, como error del contrato (con su campo en <c>data.errors</c> si lo trae).</summary>
    public static Error De(ProblemaDeCartera problema) => ErroresDeLineasDeCaptacion.De(problema);

    public static Error SinActivar(LineNature naturaleza) => naturaleza == LineNature.Contribution
        ? new ErrorConDatos(CodigoAportesSinActivar,
            "Los aportes de la cooperativa todavía no están activados en la Cartera: actívelos (o migre sus saldos) antes de abrir cuentas.",
            new { nature = naturaleza.ToString() })
        : new ErrorConDatos(CodigoCaptacionesSinActivar,
            "Las captaciones de la cooperativa todavía no están activadas en la Cartera: actívelas (o migre sus saldos) antes de abrir cuentas.",
            new { nature = naturaleza.ToString() });

    public static Error SoloAsociados(Guid persona, string nombre, string estado) => new ErrorConDatos(CodigoSoloAsociados,
        $"{nombre} no es asociado vigente y esta cuenta exige serlo: vincúlelo primero como asociado y vuelva a abrirla.",
        new { personPublicId = persona, associateStatus = estado, howToFix = "Vincular primero" });

    public static Error AportesYaAbierta(string numero) => new ErrorConDatos(CodigoAportesYaAbierta,
        $"La persona ya tiene una cuenta de aportes viva ({numero}): cada asociado tiene una sola. El reingreso abre otra después del retiro.",
        new { existingAccountNumber = numero });

    public static Error PermanenteYaAbierto(string numero) => new ErrorConDatos(CodigoPermanenteYaAbierto,
        $"La persona ya tiene un ahorro permanente vivo en esta línea ({numero}).", new { existingAccountNumber = numero });

    public static Error LineaNoAbre(string codigo, string estado) => new ErrorConDatos(CodigoLineaNoAbre,
        $"La línea {codigo} no está activa ({estado}): no abre cuentas nuevas. Actívela o elija otra.", new { code = codigo, status = estado });

    public static Error LineaSinVersion(string codigo, DateOnly fecha) => new ErrorConDatos(CodigoLineaSinVersion,
        $"La línea {codigo} no tiene condiciones que rijan el {Fecha(fecha)}: registre una versión vigente.", new { code = codigo, date = Fecha(fecha) });

    public static Error UsarRutaDeCdat() => new ErrorConDatos(CodigoUsarRutaDeCdat,
        "Un CDAT no se abre como cuenta: se constituye en «CDAT», con su plazo, su tasa y su título.", new { route = "/api/lending/term-deposits" });

    public static Error OficinaNoAdmitida(string codigo) => new ErrorConDatos(CodigoOficinaNoAdmitida,
        $"La línea {codigo} no se ofrece en esa oficina.", new { code = codigo, field = "officePublicId" });

    public static Error TitularFallecido(Guid persona) => new ErrorConDatos(CodigoTitularFallecido,
        "La persona figura como fallecida en el maestro: no se le abren cuentas.", new { personPublicId = persona });

    public static Error DatosIncompletos(Guid persona, IReadOnlyList<string> faltan) => new ErrorConDatos(CodigoDatosIncompletos,
        $"Faltan datos de conocimiento de la persona ({string.Join(", ", faltan)}): complételos en «Editar datos de la persona» y vuelva a intentar.",
        new { personPublicId = persona, missing = faltan });

    public static Error OrigenDeFondos(decimal umbral, decimal monto) => new ErrorConDatos(CodigoOrigenDeFondos,
        $"El depósito inicial de la línea ({monto:N0}) alcanza el umbral de efectivo ({umbral:N0}): adjunte la declaración de origen de fondos.",
        new { threshold = umbral, amount = monto });

    public static Error FechaDeApertura(DateOnly hoy) => new ErrorConDatos(CodigoFechaDeApertura,
        $"Una cuenta se abre con la fecha de hoy ({Fecha(hoy)}).", new { today = Fecha(hoy), field = "openingDate" });

    public static Error NoEnApertura(MemberAccountStatus estado) => new ErrorConDatos(CodigoNoEnApertura,
        $"La cuenta está «{estado}»: sólo se aprueba la apertura de una cuenta en apertura.", new { status = estado.ToString() });

    public static Error ListasBloquean(IReadOnlyList<Guid> resultados) => new ErrorConDatos(CodigoListasBloquean,
        "La persona coincide con una lista restrictiva: la apertura queda retenida hasta que el oficial de cumplimiento revise y libere la coincidencia.",
        new { screeningResultPublicIds = resultados });

    public static Error SinActualizacionPendiente() => new(CodigoSinActualizacionPendiente,
        "La cuenta no espera actualización de datos del titular.");

    public static Error PoliticaSinMarca(string politica) => new ErrorConDatos(CodigoPoliticaSinMarca,
        $"La política de exención del GMF vigente es «{politica}»: no se marca una cuenta exenta (la exención se aplica por titular o no aplica).",
        new { policy = politica });

    public static Error YaMarcada(Guid cuenta, string numero, DateOnly desde) => new ErrorConDatos(
        Domain.Entities.Lending.Deposits.Accounts.GmfExemptAccount.CodigoYaMarcada,
        $"El titular ya tiene marcada exenta del GMF la cuenta {numero} desde el {Fecha(desde)}: sólo una por persona. Para cambiarla, reemplace la vigente.",
        new { accountPublicId = cuenta, accountNumber = numero, since = Fecha(desde) });

    public static Error MarcaRetroactiva(DateOnly hoy) => new ErrorConDatos(CodigoMarcaRetroactiva,
        $"La marca del GMF no cambia hacia atrás: empieza o termina desde hoy ({Fecha(hoy)}).", new { today = Fecha(hoy) });

    public static Error MarcaTerminada(DateOnly hasta) => new ErrorConDatos(CodigoMarcaTerminada,
        $"La marca de exención ya terminó el {Fecha(hasta)}.", new { validTo = Fecha(hasta) });

    public static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
