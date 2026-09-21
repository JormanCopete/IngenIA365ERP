namespace IngenIA365ERP.Shared.Services.Nomina;

// Feature 010: lo que las cuatro liquidaciones especiales —prima, cesantías anuales, vacaciones y
// definitiva— comparten en la API (Application/Payroll/Settlements/Common/SettlementDtos.cs y
// contracts/api.md §3.1): calcular devuelve la corrida con totales, bloqueos, excluidos y avisos;
// aprobar devuelve el comprobante; reversar el espejo; descartar el motivo. Cada historia nació en
// un worktree aparte y duplicó este juego con prefijo (ResultadoPrimaDto/CesantiasCalculadaDto,
// ExcluidoDePrimaDto/CesantiasExcluidoDto/ExcluidoDto, AprobarPrimaRequest/AprobarCesantiasRequest/
// AprobarVacacionesRequest/AprobarDefinitivaRequest…); la integración de N1 los reunió aquí. Lo que
// es propio de una historia (relación de consignación, movimientos de vacaciones, descuentos de la
// definitiva) sigue en su partial. Los avisos son el mismo `AvisoCorridaDto` que ya trae el resumen
// de la corrida (`ResumenCorridaDto.Warnings`): { code, message, data }.

/// <summary>Un empleado que quedó fuera de la liquidación y por qué (<c>SettlementReasonCodes</c>).</summary>
public sealed record ExcluidoDeLiquidacionDto(Guid EmployeePublicId, string Name, string ReasonCode, string Reason)
{
    public string RazonTexto => ReasonCode switch
    {
        "SalarioIntegral" => "Salario integral",
        "AprendizLectiva" => "Aprendiz en etapa lectiva",
        "Pasante" => "Pasante sin contrato de aprendizaje",
        "YaPagadaEnDefinitiva" => "Ya pagada en la definitiva",
        "SinDiasEnElSemestre" => "Sin días en el semestre",
        "SinDiasEnElAnio" => "Sin días en el año",
        "SinDiasPendientes" => "Sin días pendientes",
        "RetiradoConDefinitiva" => "Retirado con definitiva aprobada",
        "SinSaldoDeVacaciones" => "Sin saldo de vacaciones",
        "CompensacionExcedeMaximo" => "La compensación excede el máximo",
        "LoPagaLaNominaOrdinaria" => "Lo paga la nómina ordinaria",
        "MotivoNoGeneraIndemnizacion" => "El motivo no genera indemnización",
        "SinProvisionInformada" => "Sin provisión informada",
        "ConceptoSinVersionVigente" => "Concepto sin versión vigente",
        "NoCotizaSobreEstaLiquidacion" => "No cotiza sobre esta liquidación",
        _ => ReasonCode,
    };
}

/// <summary>Lo que responde calcular o recalcular cualquiera de las cuatro: la corrida nueva, sus totales, bloqueos, excluidos y avisos.</summary>
public sealed record LiquidacionCalculadaDto(
    Guid RunPublicId,
    int Version,
    string Kind,
    DateOnly CutoffDate,
    int Employees,
    TotalesCorridaDto Totals,
    IReadOnlyList<BloqueoDto> Blockers,
    IReadOnlyList<ExcluidoDeLiquidacionDto> Excluded,
    IReadOnlyList<AvisoCorridaDto> Warnings)
{
    /// <summary>La frase que las pantallas muestran al terminar el cálculo: «Calculada v2: 12 empleado(s), neto 3.400.000, 1 excluido(s), 2 bloqueo(s).»</summary>
    public string Resumen(string verbo) =>
        $"{verbo} v{Version}: {Employees} empleado(s), neto {Totals.Net:N0}"
        + (Excluded.Count > 0 ? $", {Excluded.Count} excluido(s)" : string.Empty)
        + (Blockers.Count > 0 ? $", {Blockers.Count} bloqueo(s)" : string.Empty) + ".";
}

/// <summary>
/// Cuerpo de <c>POST …/{runId}/approve</c> de las cuatro. <c>Confirm</c> es la confirmación explícita;
/// <c>PostingDate</c> la fecha del comprobante (por defecto el corte, D-04/D-21); <c>ConfirmEmpty</c>
/// acepta aprobar sin empleados (prima, cesantías, vacaciones); <c>ConfirmWithoutSegregation</c> la
/// segunda confirmación cuando quien calculó aprueba; <c>PayDate</c> sólo la leen las cesantías (pago de
/// los intereses); <c>AcceptRetroactive</c> sólo las vacaciones (disfrute sobre un período aprobado).
/// Un campo que una liquidación no lee se ignora en la API.
/// </summary>
public sealed record AprobarLiquidacionRequest(
    bool Confirm,
    DateOnly? PostingDate = null,
    bool ConfirmEmpty = false,
    bool ConfirmWithoutSegregation = false,
    DateOnly? PayDate = null,
    bool AcceptRetroactive = false);

/// <summary>Cuerpo de reversar y descartar: el motivo, obligatorio.</summary>
public sealed record MotivoDeLiquidacionRequest(string Reason);

/// <summary>Resultado de aprobar: el comprobante NM, el total y si se aprobó sin segregación.</summary>
public sealed record LiquidacionAprobadaDto(Guid RunPublicId, Guid DocumentPublicId, string Number, decimal Total, DateOnly PostingDate, bool ApprovedWithoutSegregation);

/// <summary>Resultado de reversar: el comprobante espejo.</summary>
public sealed record LiquidacionReversadaDto(Guid RunPublicId, Guid ReversalDocumentPublicId, string ReversalNumber);

/// <summary>Resultado de descartar el borrador.</summary>
public sealed record LiquidacionDescartadaDto(Guid RunPublicId, string Reason);
