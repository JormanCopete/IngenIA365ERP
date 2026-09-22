using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Pila;

/// <summary>
/// La validación previa de la planilla con la taxonomía de Aportes en Línea (feature 010,
/// FR-025; research R9): <b>bloqueante</b> lo que el operador devuelve como Error (sin EPS,
/// AFP, ARL o CCF, sin código PILA, sin DIVIPOLA, sin actividad económica, documento de
/// longitud inválida, tarifa sin vigencia, datos del aportante incompletos) y <b>alerta</b> lo
/// que deja cargar (segundo apellido faltante, régimen de transición desconocido, layout sin
/// cotejar). Cada hallazgo trae el campo, el empleado y la ruta donde se corrige.
/// </summary>
public static class PilaValidator
{
    /// <summary>Longitudes máximas (o exactas) del número de identificación por tipo (Res. 1529/2026), exigibles para pagos desde el 01-10-2026.</summary>
    public static readonly IReadOnlyDictionary<string, (int Max, bool Exact)> DocumentLengths = new Dictionary<string, (int, bool)>(StringComparer.OrdinalIgnoreCase)
    {
        ["CC"] = (10, false), ["TI"] = (11, false), ["CE"] = (7, false), ["PA"] = (16, false), ["PE"] = (15, true), ["PT"] = (8, false),
        ["CD"] = (16, false), ["SC"] = (16, false), ["NI"] = (16, false),
    };

    public static readonly DateOnly DocumentLengthRuleFrom = new(2026, 10, 1);

    public static IReadOnlyList<PilaIssueItem> Validate(PilaInput input)
    {
        var issues = new List<PilaIssueItem>();
        var e = input.Employer;
        const string settingsRoute = "/nomina/pila?ajustes=1";

        // --- aportante ---
        if (string.IsNullOrWhiteSpace(e.Nit) || string.IsNullOrWhiteSpace(e.Name))
            issues.Add(new(PilaIssueSeverity.Blocking, "Pila.AportanteSinNit", "La empresa no tiene NIT o razón social registrados (Maestros › Empresas).", 6, LinkRoute: "/maestros/empresas"));
        if (string.IsNullOrWhiteSpace(e.ArlPilaCode))
            issues.Add(new(PilaIssueSeverity.Blocking, "Pila.AportanteSinArl", "Falta el código PILA de la ARL del aportante en los datos del aportante.", 14, LinkRoute: settingsRoute));
        if (string.IsNullOrWhiteSpace(e.OperatorCode))
            issues.Add(new(PilaIssueSeverity.Blocking, "Pila.AportanteSinOperador", "Falta el código del operador de información (campo 22) en los datos del aportante.", 22, LinkRoute: settingsRoute));
        if (e.PresentationForm == "S" && string.IsNullOrWhiteSpace(e.BranchCode))
            issues.Add(new(PilaIssueSeverity.Blocking, "Pila.AportanteSinSucursal", "La forma de presentación es por sucursal y falta el código de la sucursal.", 12, LinkRoute: settingsRoute));

        // --- parámetros ---
        var faltantes = input.Parameters.Missing(PilaParameterCodes.Required);
        if (faltantes.Count > 0)
            issues.Add(new(PilaIssueSeverity.Blocking, "Pila.TarifaSinVigencia",
                $"Sin vigencia al {input.MonthStart:dd/MM/yyyy} de: {string.Join(", ", faltantes)}. Cárguelos en Nómina › Parámetros legales.", LinkRoute: "/nomina/parametros-legales"));

        // --- layout ---
        if (!input.Layout.IsVerified)
            issues.Add(new(PilaIssueSeverity.Warning, "Pila.LayoutSinCotejar",
                $"El layout {input.Layout.Code} tiene campos sin cotejar contra el anexo v30 y una planilla pagada (T094): revise el archivo en el validador del operador antes de pagar."));

        // --- cotizantes ---
        foreach (var c in input.Contributors)
        {
            var ruta = $"/nomina/empleados/{c.EmployeePublicId}";
            var nombre = c.FullName;
            void Bloquea(string code, string msg, byte? field, string? link = null) => issues.Add(new(PilaIssueSeverity.Blocking, code, $"{nombre}: {msg}", field, c.EmployeeId, c.EmployeePublicId, nombre, link ?? ruta));
            void Alerta(string code, string msg, byte? field, string? link = null) => issues.Add(new(PilaIssueSeverity.Warning, code, $"{nombre}: {msg}", field, c.EmployeeId, c.EmployeePublicId, nombre, link ?? ruta));

            var (tipo, _) = PilaBuilder.TipoDeCotizante(c);
            var aprendiz = tipo == "19";

            if (!c.HasHealthProvider) Bloquea("Pila.SinEps", "la ficha no tiene EPS.", 33);
            else if (string.IsNullOrWhiteSpace(c.HealthPilaCode)) Bloquea("Pila.SinCodigoPila", "la EPS de la ficha no tiene código PILA (Nómina › EPS).", 33, "/nomina/eps");

            if (!aprendiz && !c.ForeignNotRequiredToContributePension && c.Class != EmployeeClass.Pensioner)
            {
                if (!c.HasPensionProvider) Bloquea("Pila.SinAfp", "la ficha no tiene fondo de pensiones.", 31);
                else if (string.IsNullOrWhiteSpace(c.PensionPilaCode)) Bloquea("Pila.SinCodigoPila", "el fondo de pensiones de la ficha no tiene código PILA (Nómina › Pensiones).", 31, "/nomina/pensiones");
            }

            if (!c.HasWorkRiskProvider) Bloquea("Pila.SinArl", "la ficha no tiene ARL.", 77);
            else if (string.IsNullOrWhiteSpace(c.WorkRiskPilaCode)) Bloquea("Pila.SinCodigoPila", "la ARL de la ficha no tiene código PILA (Nómina › ARL).", 77, "/nomina/arl");
            if (c.WorkRiskClass is null) Bloquea("Pila.SinClaseRiesgo", "la ficha no tiene clase de riesgo ARL.", 78);

            if (!aprendiz)
            {
                if (!c.HasFamilyCompensationFund) Bloquea("Pila.SinCcf", "la ficha no tiene caja de compensación.", 35);
                else if (string.IsNullOrWhiteSpace(c.FamilyCompensationPilaCode)) Bloquea("Pila.SinCodigoPila", "la caja de compensación de la ficha no tiene código PILA (Nómina › Cajas de compensación).", 35, "/nomina/cajas-compensacion");
            }

            var dane = c.MunicipalityDaneCode ?? e.DefaultMunicipalityDaneCode;
            if (string.IsNullOrWhiteSpace(dane) || dane.Trim().Length < 5)
                Bloquea("Pila.SinDivipola", "sin departamento y municipio DIVIPOLA de la ubicación laboral (ni en la ficha ni en los datos del aportante).", 9);
            if (string.IsNullOrWhiteSpace(c.EconomicActivityCode ?? e.DefaultEconomicActivityCode))
                Bloquea("Pila.SinActividadEconomica", "sin código de actividad económica ARL (Decreto 768/2022), ni en la ficha ni en los datos del aportante.", 98);

            if (string.IsNullOrWhiteSpace(c.Document) || string.IsNullOrWhiteSpace(c.FirstLastName) || string.IsNullOrWhiteSpace(c.FirstName))
                Bloquea("Pila.DatosPersonalesIncompletos", "la persona no tiene documento, primer apellido o primer nombre.", 4, $"/maestros/personas");
            else if (DocumentLengths.TryGetValue(c.DocumentType, out var regla) && DateOnly.FromDateTime(input.MonthStart) >= DocumentLengthRuleFrom)
            {
                var largo = c.Document.Trim().Length;
                if (regla.Exact ? largo != regla.Max : largo > regla.Max)
                    Bloquea("Pila.DocumentoLargo", $"el documento {c.DocumentType} {c.Document} tiene {largo} caracteres y la Res. 1529/2026 admite {(regla.Exact ? "exactamente" : "hasta")} {regla.Max}.", 4);
            }
            if (c.Document?.Trim().Length > 16) Bloquea("Pila.DocumentoLargo", "el documento supera las 16 posiciones del campo.", 4);

            if (string.IsNullOrWhiteSpace(c.SecondLastName))
                Alerta("Pila.SegundoApellidoFaltante", "sin segundo apellido: el operador lo marca como alerta si la persona lo tiene.", 12);

            if (c.TransitionRegime == PensionTransitionRegime.Unknown && input.MonthStart >= new DateTime(2027, 4, 1) && !aprendiz && c.Class != EmployeeClass.Pensioner)
                Alerta("Pila.RegimenTransicionDesconocido", "no se sabe si está en régimen de transición de la Ley 2381/2024: el fondo de solidaridad se liquida con la tabla vigente.", 51);

            if (c.BasicSalary <= 0m)
                Bloquea("Pila.SinSalario", "la ficha no tiene salario vigente.", 40);
        }

        return issues;
    }
}
