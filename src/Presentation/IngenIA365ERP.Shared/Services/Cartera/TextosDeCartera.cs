using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de lo que la API de Cartera devuelve como número o código (feature 013, fase 4, T0315; US17): los enums de gobierno,
/// de sesiones, de vinculados y de mensajes al deudor, con la numeración de <c>Domain/Enums/Lending</c>. Las listas
/// (<see cref="ClasesDeOrgano"/>, <see cref="Canales"/>…) alimentan los desplegables con su valor numérico, que la API acepta igual que el
/// nombre. (nuevo)
/// </summary>
public static class TextosDeCartera
{
    private static string Numero(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static string De(IReadOnlyList<(int Valor, string Texto)> lista, int valor) =>
        lista.FirstOrDefault(p => p.Valor == valor).Texto ?? Numero(valor);

    // ---------------------------------------------------------------------------------------------- gobierno --

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeNivel { get; } = [(1, "Individual"), (2, "Colegiado")];

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeOrgano { get; } =
        [(1, "Consejo de administración"), (2, "Junta de vigilancia"), (3, "Comité de crédito"), (4, "Comité de riesgos"), (5, "Gerencia"), (99, "Otro")];

    public static IReadOnlyList<(int Valor, string Texto)> PapelesDeMiembro { get; } = [(1, "Presidente"), (2, "Secretario"), (3, "Miembro"), (4, "Suplente")];

    public static IReadOnlyList<(int Valor, string Texto)> Comparaciones { get; } = [(1, "al menos"), (2, "más de")];

    public static IReadOnlyList<(int Valor, string Texto)> BasesDeMayoria { get; } = [(1, "presentes con voto"), (2, "composición del órgano")];

    public static IReadOnlyList<(int Valor, string Texto)> Garantias { get; } =
        [(1, "Pagaré"), (2, "Codeudor"), (3, "Prenda"), (4, "Hipoteca"), (5, "Fondo de garantías"), (6, "Aportes")];

    /// <summary>Los sujetos de una decisión de Cartera (<c>LendingDecisionSubjects</c>): viajan como texto.</summary>
    public static IReadOnlyList<(string Codigo, string Texto)> Sujetos { get; } =
    [
        ("CreditApplication", "Solicitud de crédito"), ("CreditLimit", "Cupo"), ("Novelty", "Novedad"), ("Condonation", "Condonación"),
        ("WriteOffBatch", "Lote de castigo"), ("PortfolioEvaluation", "Evaluación de cartera"), ("ImpairmentModelChange", "Cambio del modelo de deterioro"),
        ("ActivationDifferences", "Diferencias de la activación"), ("ValidationFailureException", "Excepción a una validación"),
        ("RiskPolicyVersion", "Versión de la política de riesgo"),
    ];

    /// <summary>Mecanismos de una novedad (<c>NoveltyMechanism</c>), para las reglas de la matriz (K3, T0879).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> MecanismosDeNovedad { get; } =
        [(1, "Prórroga"), (2, "Cambio de fecha de pago"), (3, "Cambio de tasa"), (4, "Novación"), (5, "Refinanciación"), (6, "Cambio de oficina")];

    /// <summary>Causas de una novedad (<c>NoveltyCause</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> CausasDeNovedad { get; } = [(1, "Reestructuración"), (2, "Modificación")];

    public static string MecanismoDeNovedad(int v) => De(MecanismosDeNovedad, v);

    public static string CausaDeNovedad(int v) => De(CausasDeNovedad, v);

    public static string Sujeto(string codigo) => Sujetos.FirstOrDefault(s => s.Codigo == codigo).Texto ?? codigo;

    public static string ClaseDeNivel(int v) => De(ClasesDeNivel, v);

    public static string ClaseDeOrgano(int v) => De(ClasesDeOrgano, v);

    public static string Papel(int v) => De(PapelesDeMiembro, v);

    public static string Garantia(int v) => De(Garantias, v);

    /// <summary>«más de 1/2», «al menos 2/3».</summary>
    public static string Fraccion(FraccionDto? f) => f is null ? "—" : $"{De(Comparaciones, f.Comparison)} {f.Numerator}/{f.Denominator}";

    public static string BaseDeMayoria(int v) => De(BasesDeMayoria, v);

    public static string RazonDeEnrutamiento(int v) => v switch
    {
        1 => "Tope de monto",
        2 => "Regla de la matriz",
        3 => "Persona vinculada",
        4 => "Excepción a la política",
        _ => Numero(v),
    };

    public static string NoExceptuable(int v) => v switch
    {
        1 => "Capacidad de pago",
        2 => "Autorización del titular",
        3 => "Coincidencia en listas restrictivas",
        4 => "Usura",
        _ => Numero(v),
    };

    // ---------------------------------------------------------------------------------------------- sesiones --

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeSesion { get; } = [(1, "Ordinaria"), (2, "Extraordinaria")];

    public static IReadOnlyList<(int Valor, string Texto)> Modalidades { get; } = [(1, "Presencial"), (2, "Virtual"), (3, "Mixta")];

    public static IReadOnlyList<(int Valor, string Texto)> EstadosDeSesion { get; } = [(1, "Borrador"), (2, "Abierta"), (3, "Cerrada")];

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDePunto { get; } = [(1, "Decisión"), (2, "Recomendación")];

    public static IReadOnlyList<(int Valor, string Texto)> Votos { get; } = [(1, "A favor"), (2, "En contra"), (3, "Abstención")];

    public static IReadOnlyList<(int Valor, string Texto)> MetodosDeVoto { get; } =
        [(1, "Registrado por secretaría"), (2, "En su propia sesión"), (3, "Presente con su llave"), (4, "Presente con código")];

    public static IReadOnlyList<(int Valor, string Texto)> Decisiones { get; } =
        [(1, "Aprobar"), (2, "Aprobar con condiciones"), (3, "Negar"), (4, "Devolver")];

    public static string ClaseDeSesion(int v) => De(ClasesDeSesion, v);

    public static string Modalidad(int v) => De(Modalidades, v);

    public static string EstadoDeSesion(int v) => De(EstadosDeSesion, v);

    public static string ClaseDePunto(int v) => De(ClasesDePunto, v);

    public static string Voto(int v) => De(Votos, v);

    public static string MetodoDeVoto(int v) => De(MetodosDeVoto, v);

    public static string Decision(int? v) => v is { } d ? De(Decisiones, d) : "Sin decidir";

    public static string Asistencia(int v) => v switch
    {
        1 => "Presente",
        2 => "Ausente",
        3 => "Excusado",
        _ => Numero(v),
    };

    public static string ReglaDeVotacion(int v) => v switch
    {
        1 => "Mayoría",
        2 => "Mayoría calificada de vinculados",
        _ => Numero(v),
    };

    /// <summary>Por qué una persona no vota un punto.</summary>
    public static string Exclusion(string motivo) => motivo switch
    {
        "Applicant" => "Es el solicitante",
        "Filer" => "Radicó la solicitud",
        "Interested" => "Tiene interés en el asunto",
        "NotMember" => "No es miembro del órgano",
        "Absent" => "No asistió",
        _ => motivo,
    };

    // -------------------------------------------------------------------------------------------- vinculados --

    public static IReadOnlyList<(int Valor, string Texto)> Fundamentos { get; } =
    [
        (1, "Miembro del consejo de administración"), (2, "Miembro de la junta de vigilancia"), (3, "Representante legal"),
        (4, "Aportante significativo"), (5, "Cónyuge o compañero permanente"), (6, "Pariente"),
    ];

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeParentesco { get; } = [(1, "Consanguinidad"), (2, "Afinidad"), (3, "Civil")];

    public static string Fundamento(int v) => De(Fundamentos, v);

    public static string Parentesco(int? clase, byte? grado) => clase is { } c ? $"{De(ClasesDeParentesco, c)}{(grado is { } g ? $", grado {g}" : "")}" : "—";

    public static string OrigenDelVinculo(int v) => v switch
    {
        1 => "Manual",
        2 => "Plantilla",
        3 => "Migración",
        _ => Numero(v),
    };

    // ---------------------------------------------------------------------------------------------- mensajes --

    public static IReadOnlyList<(int Valor, string Texto)> Canales { get; } = [(1, "Correo"), (2, "SMS"), (3, "WhatsApp"), (4, "Carta")];

    public static IReadOnlyList<(int Valor, string Texto)> Propositos { get; } =
        [(1, "Cobranza"), (2, "Aviso previo"), (3, "Informativo"), (4, "Entrega de documentos"), (5, "Aviso de gastos de cobranza")];

    public static IReadOnlyList<(int Valor, string Texto)> EstadosDeMensaje { get; } =
        [(1, "En cola"), (2, "Diferido"), (3, "Excluido"), (4, "Enviado"), (5, "Entregado"), (6, "Fallido"), (7, "Simulado"), (8, "Cancelado")];

    public static string Canal(int v) => De(Canales, v);

    public static string Proposito(int v) => De(Propositos, v);

    public static string EstadoDeMensaje(int v) => De(EstadosDeMensaje, v);

    /// <summary>Un mensaje se puede cancelar mientras no haya salido: en cola o diferido.</summary>
    public static bool SePuedeCancelar(int estado) => estado is 1 or 2;

    public static string RolDelDestinatario(int v) => v switch
    {
        1 => "Deudor",
        2 => "Codeudor",
        3 => "Pagador",
        _ => Numero(v),
    };

    public static string DecisionDeContacto(string decision) => decision switch
    {
        "Permitido" or "Allowed" => "Permitido",
        "Diferido" or "Deferred" => "Diferido",
        "Excluido" or "Excluded" => "Excluido",
        _ => decision,
    };

    /// <summary>Las razones de la ventana legal de contacto (Ley 2300, <c>ContactWindowReason</c>).</summary>
    public static string RazonDeContacto(string razon) => razon switch
    {
        "RecipientNotObligated" => "El destinatario no está obligado en el crédito",
        "DebtorDeceased" => "El deudor falleció",
        "Insolvency" => "El deudor está en insolvencia",
        "CollectionSuspended" => "La cobranza está suspendida",
        "ChannelNotAuthorized" => "El titular no autorizó ese canal",
        "AuthorizationWithoutEvidence" => "La autorización no tiene evidencia",
        "NoDestination" => "No hay dirección o número de contacto",
        "Sunday" => "Es domingo",
        "Holiday" => "Es festivo",
        "OutsideHours" => "Fuera del horario permitido",
        "DailyLimitReached" => "Ya se alcanzó el límite de contactos del día",
        "WeeklyChannelLocked" => "El canal ya se usó esta semana",
        "NoWindow" => "No hay ventana disponible en los próximos días",
        "ChannelUnavailable" => "El canal no está disponible",
        _ => razon,
    };

    /// <summary>
    /// La lista blanca de variables de cada propósito (las comunes más las suyas), espejo de <c>RenderizadorDePlantillasDeMensaje</c>; la
    /// API la vuelve a revisar al guardar y en la vista previa.
    /// </summary>
    public static IReadOnlyList<string> VariablesPermitidas(int proposito)
    {
        string[] comunes = ["NombreDelTitular", "NombreDeLaCooperativa", "TelefonoDeLaCooperativa", "NumeroDelCredito", "FechaDeCorte"];
        string[] propias = proposito switch
        {
            1 => ["ValorVencido", "DiasDeMora", "CuotasVencidas", "ValorParaPonerseAlDia"],
            2 => ["ValorDeLaObligacion", "ValorVencido", "FechaMasTempranaDeReporte", "Centrales"],
            // Feature 014 (D35, 2026-10-18): los avisos de captaciones (CAMBIOTASA, MAYOREDAD, CONTRATINC) son plantillas estándar que cada entidad edita aquí.
            3 => ["ValorAplicado", "ModalidadAplicada", "NuevaCuota", "NuevoPlazo", "MotivoDeNoDescuento", "FormaDePagoNueva",
                "NumeroDeCuenta", "NombreDeLaLinea", "TasaAnterior", "TasaNueva", "FechaDelCambio", "FechaDeMayoriaDeEdad",
                // US7 (T182): el aviso del incumplimiento del ahorro contractual (CONTRATINC).
                "CuotasAtrasadasDelContrato", "FechaDeIncumplimiento", "EfectoDelIncumplimiento",
                // US14 (T208, T209): los avisos de la inactividad (PREINACTIV, INACTIVA) y del traslado a no reclamados (NORECLAMA).
                "FechaDeInactividad", "DiasDeInactividad", "ValorNoReclamado", "FechaDelTraslado"],
            4 => ["ClaseDeDocumento", "NumeroDelDocumento", "CodigoDeVerificacion"],
            5 => ["ValorDelGasto", "Etapa", "FechaDeCausacion"],
            _ => [],
        };
        return [.. comunes, .. propias];
    }

    // --------------------------------------------------------------------------------------- autorizaciones --

    public static IReadOnlyList<(int Valor, string Texto)> Finalidades { get; } =
    [
        (1, "Tratamiento general"), (2, "Trámite de crédito"), (3, "Consulta en centrales"), (4, "Reporte a centrales"), (5, "Canal de contacto"),
    ];

    public static IReadOnlyList<(int Valor, string Texto)> CanalesDeAutorizacion { get; } =
        [(1, "SMS"), (2, "WhatsApp"), (3, "Correo"), (4, "Llamada"), (5, "Visita")];

    public static string Finalidad(int v) => De(Finalidades, v);

    public static string CanalDeAutorizacion(int? v) => v is { } c ? De(CanalesDeAutorizacion, c) : "—";

    public static string OrigenDeAutorizacion(int v) => v switch
    {
        1 => "Registro de la persona",
        2 => "Solicitud de crédito",
        3 => "Migración",
        4 => "Solicitud del titular",
        5 => "Plantilla",
        _ => Numero(v),
    };

    public static string EstadoDeAutorizacion(string estado) => estado switch
    {
        "Active" => "Vigente",
        "Declined" => "Rechazada",
        "Revoked" => "Revocada",
        "Replaced" => "Reemplazada",
        _ => estado,
    };

    /// <summary>Una autorización migrada (o de cualquier origen) sin evidencia no habilita centrales ni cobranza (FR-006).</summary>
    public static bool BloqueaPorFaltaDeEvidencia(AutorizacionDeDatosDto a) => a.Evidence is null && a.Status == "Active";

    // ---------------------------------------------------------------------------------------------- reclamos --

    public static string ClaseDeReclamo(int v) => v == 2 ? "Suplantación" : "Reclamo";

    public static string EstadoDeReclamo(int v) => v switch
    {
        1 => "En trámite",
        2 => "Respondido",
        3 => "Aceptado tácitamente",
        4 => "Desistido",
        _ => Numero(v),
    };

    /// <summary>«K2», «K6a»: la entrega de Cartera de una plantilla (1 = K1 … 6 = K6a, 7 = K6b).</summary>
    public static string Entrega(int v) => v switch
    {
        6 => "K6a",
        7 => "K6b",
        _ => $"K{Numero(v)}",
    };

    // ------------------------------------------------------------------------------------- líneas de crédito (US1) --

    public static IReadOnlyList<(int Valor, string Texto)> EstadosDeLinea { get; } = [(1, "Borrador"), (2, "Activa"), (3, "Cerrada")];

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeTasa { get; } = [(1, "Fija"), (2, "Variable")];

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeMora { get; } = [(1, "Fija (EA)"), (2, "Relativa a la corriente")];

    public static IReadOnlyList<(int Valor, string Texto)> Convenciones { get; } = [(1, "30/360"), (2, "Real/365"), (3, "Real/360")];

    public static IReadOnlyList<(int Valor, string Texto)> Equivalencias { get; } = [(1, "Compuesta"), (2, "Lineal")];

    public static IReadOnlyList<(int Valor, string Texto)> Sistemas { get; } = [(1, "Cuota fija"), (2, "Abono constante a capital"), (3, "Pago único")];

    public static IReadOnlyList<(int Valor, string Texto)> Periodicidades { get; } =
    [
        (0, "Pago único"), (7, "Semanal"), (10, "Decadal"), (15, "Quincenal"), (30, "Mensual"), (60, "Bimestral"), (90, "Trimestral"),
        (180, "Semestral"), (360, "Anual"),
    ];

    public static IReadOnlyList<(int Valor, string Texto)> FormasDePago { get; } =
        [(1, "Caja"), (2, "Nómina propia"), (3, "Libranza (pagaduría)"), (4, "Débito automático")];

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeGracia { get; } = [(0, "Sin gracia"), (1, "A capital"), (2, "Total")];

    public static IReadOnlyList<(int Valor, string Texto)> InteresesEnGracia { get; } = [(1, "En la primera cuota"), (2, "Distribuido")];

    public static IReadOnlyList<(int Valor, string Texto)> ModosDeRedondeo { get; } =
        [(1, "Al más cercano"), (2, "Al par"), (3, "Hacia arriba"), (4, "Hacia abajo")];

    public static IReadOnlyList<(int Valor, string Texto)> MomentosDeRedondeo { get; } = [(1, "Por concepto"), (2, "Por cuota"), (3, "Por total")];

    public static IReadOnlyList<(int Valor, string Texto)> Prelaciones { get; } = [(1, "Cuota más antigua primero"), (2, "Concepto primero")];

    public static IReadOnlyList<(int Valor, string Texto)> Recalculos { get; } = [(1, "Reducir el plazo"), (2, "Reducir la cuota")];

    public static IReadOnlyList<(int Valor, string Texto)> TiposDeDeudor { get; } = [(1, "Asociado"), (2, "Cliente")];

    public static IReadOnlyList<(int Valor, string Texto)> BasesDeSeguro { get; } = [(1, "Saldo"), (2, "Monto inicial")];

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeEquivalencia { get; } = [(1, "Periodicidad"), (2, "Unidad de plazo")];

    public static string EstadoDeLinea(int v) => De(EstadosDeLinea, v);

    public static string ClaseDeTasa(int v) => De(ClasesDeTasa, v);

    public static string ClaseDeMora(int v) => De(ClasesDeMora, v);

    public static string Convencion(int v) => De(Convenciones, v);

    public static string Sistema(int v) => De(Sistemas, v);

    public static string Periodicidad(int v) => De(Periodicidades, v);

    public static string FormaDePago(int v) => De(FormasDePago, v);

    public static string ClaseDeGracia(int v) => De(ClasesDeGracia, v);

    public static string TipoDeDeudor(int? v) => v is { } x ? De(TiposDeDeudor, x) : "Cualquiera";

    public static string BaseDeSeguro(int v) => De(BasesDeSeguro, v);

    public static string ClaseDeEquivalencia(int v) => De(ClasesDeEquivalencia, v);

    /// <summary>«Manual» o «Modalidad retirada del catálogo nacional».</summary>
    public static string MotivoDeCierreDeLinea(int? v) => v switch
    {
        1 => "Cierre manual",
        2 => "Modalidad retirada del catálogo nacional",
        _ => "",
    };

    /// <summary>Una lista de valores de un catálogo, separados por coma.</summary>
    public static string Lista(IReadOnlyList<(int Valor, string Texto)> catalogo, IEnumerable<int> valores) =>
        string.Join(", ", valores.Select(v => De(catalogo, v)));
}
