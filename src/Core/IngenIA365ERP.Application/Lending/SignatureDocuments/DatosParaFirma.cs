using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.SignatureDocuments;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Domain.Lending.SignatureDocuments.CamposDelSistema;

namespace IngenIA365ERP.Application.Lending.SignatureDocuments;

/// <summary>Los campos propios de la cooperativa: el catálogo para validar y los valores de los fijos y de los digitados.</summary>
public sealed class CamposPropios(IReadOnlyList<SignatureDocumentField> campos)
{
    public IReadOnlyList<SignatureDocumentField> Campos { get; } = campos;

    public static async Task<CamposPropios> LeerAsync(IApplicationDbContext db, CancellationToken ct) =>
        new(await db.SignatureDocumentFields.AsNoTracking().Where(f => !f.IsDeleted && f.IsActive).OrderBy(f => f.Order).ThenBy(f => f.Code).ToListAsync(ct));

    /// <summary>Los códigos que puede usar una plantilla (los de pesos también con <c>_LETRAS</c>).</summary>
    public IEnumerable<string> Codigos => Campos.SelectMany(f =>
        f.Kind == SignatureFieldKind.Money ? new[] { f.Code, f.Code + C.SufijoLetras } : [f.Code]);

    public CatalogoDeCampos Catalogo => new(Codigos);

    /// <summary>
    /// Los valores con formato: los fijos con el suyo y los digitados con lo que llegó (o su valor propuesto). Un valor que no es del tipo del campo
    /// es un error.
    /// </summary>
    public Result<Dictionary<string, string?>> Valores(IReadOnlyDictionary<string, string?>? digitados)
    {
        var valores = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var f in Campos)
        {
            var crudo = f.Source == SignatureFieldSource.Constant
                ? f.ConstantValue
                : digitados is not null && digitados.TryGetValue(f.Code, out var d) && !string.IsNullOrWhiteSpace(d) ? d : f.DefaultValue;
            var formateado = Formatear(f, crudo);
            if (formateado.IsFailure) return Result.Failure<Dictionary<string, string?>>(formateado.Error);
            valores[f.Code] = formateado.Value.Valor;
            if (f.Kind == SignatureFieldKind.Money) valores[f.Code + C.SufijoLetras] = formateado.Value.Letras;
        }

        return Result.Success(valores);
    }

    private static Result<(string? Valor, string? Letras)> Formatear(SignatureDocumentField f, string? crudo)
    {
        if (string.IsNullOrWhiteSpace(crudo)) return Result.Success<(string?, string?)>((null, null));
        var texto = crudo.Trim();
        switch (f.Kind)
        {
            case SignatureFieldKind.Money:
                var limpio = texto.Replace("$", "").Replace(" ", "").Replace(".", "").Replace(",", ".");
                if (!decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var pesos) || pesos < 0)
                    return Result.Failure<(string?, string?)>(ErroresDeDocumentosParaFirma.ValorInvalido(f.Label, "un valor en pesos"));
                return Result.Success<(string?, string?)>((FormatoDeFirma.Pesos(pesos), FormatoDeFirma.Letras(pesos)));
            case SignatureFieldKind.Number:
                if (!decimal.TryParse(texto.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    return Result.Failure<(string?, string?)>(ErroresDeDocumentosParaFirma.ValorInvalido(f.Label, "un número"));
                return Result.Success<(string?, string?)>((texto, null));
            case SignatureFieldKind.Date:
                if (!DateOnly.TryParseExact(texto, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                    return Result.Failure<(string?, string?)>(ErroresDeDocumentosParaFirma.ValorInvalido(f.Label, "una fecha (dd/mm/aaaa)"));
                return Result.Success<(string?, string?)>((FormatoDeFirma.Fecha(fecha), null));
            case SignatureFieldKind.YesNo:
                return Result.Success<(string?, string?)>((PlantillaDeFirma.EsVerdadero(texto) ? "SI" : "NO", null));
            default:
                return Result.Success<(string?, string?)>((texto, null));
        }
    }
}

/// <summary>Cómo se imprimen los valores en los documentos para firma.</summary>
public static class FormatoDeFirma
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static string Pesos(decimal valor) => "$ " + decimal.Round(valor, 0, MidpointRounding.AwayFromZero).ToString("N0", Co);

    public static string Letras(decimal valor) => ValorEnLetras.Pesos(decimal.Round(valor, 0, MidpointRounding.AwayFromZero)).ToUpper(Co);

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string FechaLarga(DateOnly fecha) => $"{fecha.Day} de {Mes(fecha)} de {fecha.Year}";

    public static string Mes(DateOnly fecha) => Co.DateTimeFormat.GetMonthName(fecha.Month).ToLower(Co);

    /// <summary>Una tasa como fracción (0,24) en porcentaje con dos decimales («24,00 %»).</summary>
    public static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("N2", Co) + " %";

    public static string Periodicidad(RepaymentPeriodicity? p) => p switch
    {
        RepaymentPeriodicity.SinglePayment => "pago único",
        RepaymentPeriodicity.Weekly => "semanal",
        RepaymentPeriodicity.TenDay => "decadal",
        RepaymentPeriodicity.Biweekly => "quincenal",
        RepaymentPeriodicity.Monthly => "mensual",
        RepaymentPeriodicity.Bimonthly => "bimestral",
        RepaymentPeriodicity.Quarterly => "trimestral",
        RepaymentPeriodicity.SemiAnnual => "semestral",
        RepaymentPeriodicity.Annual => "anual",
        _ => string.Empty,
    };

    public static string Sistema(AmortizationSystem? s) => s switch
    {
        AmortizationSystem.LevelPayment => "cuota fija",
        AmortizationSystem.ConstantPrincipal => "abono constante a capital",
        AmortizationSystem.Bullet => "pago único de capital al vencimiento",
        _ => string.Empty,
    };

    public static string Canal(RepaymentChannel c) => c switch
    {
        RepaymentChannel.Cashier => "pago directo en caja",
        RepaymentChannel.OwnPayroll => "descuento por nómina de la cooperativa",
        RepaymentChannel.PaymasterDeduction => "descuento por libranza a través de la pagaduría",
        RepaymentChannel.DirectDebit => "débito automático",
        _ => string.Empty,
    };

    /// <summary>Un número de documento con puntos de miles si es numérico (31.234.567); si lleva letras, tal cual.</summary>
    public static string Documento(string? numero) =>
        string.IsNullOrWhiteSpace(numero) ? string.Empty
        : numero.All(char.IsDigit) && long.TryParse(numero, out var n) ? n.ToString("N0", Co) : numero.Trim();
}

/// <summary>Lo que se armó de la solicitud: los datos de las plantillas, quienes firman y los avisos (lo que no se pudo calcular).</summary>
public sealed record DatosDeLaSolicitud(
    DatosDePlantilla Datos,
    FirmanteParaImprimir Deudor,
    IReadOnlyList<FirmanteParaImprimir> Codeudores,
    string? CodigoDeLinea,
    IReadOnlyList<string> Avisos);

/// <summary>
/// Arma los campos del sistema de los documentos para firma desde una solicitud (2026-11-18): la cooperativa, la oficina, la fecha, las condiciones
/// aprobadas (el monto y las cuotas aprobados; la cuota, las tasas y las fechas, de la misma simulación que usa el desembolso), el deudor, los
/// codeudores y la pagaduría. Sólo lee. Lo que no puede calcular lo deja vacío y lo avisa: el documento sale con el espacio para llenar a mano.
/// (nuevo)
/// </summary>
public sealed class ArmadoDeDatosParaFirma(IApplicationDbContext db, SimuladorDeCredito simulador, IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<DatosDeLaSolicitud> ArmarAsync(CreditApplication s, string? numeroDePagare, IReadOnlyDictionary<string, string?> propios,
        CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var avisos = new List<string>();
        var campos = new Dictionary<string, string?>(StringComparer.Ordinal);

        var (cooperativa, nit) = await MembreteDeCartera.CooperativaAsync(db, ct);
        campos[C.CooperativaNombre] = cooperativa;
        campos[C.CooperativaNit] = nit;
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == s.BranchId).Select(b => new { b.Name, b.MunicipalityDaneCode }).FirstOrDefaultAsync(ct);
        campos[C.Oficina] = oficina?.Name;
        if (oficina?.MunicipalityDaneCode is { Length: > 0 } dane)
            campos[C.OficinaCiudad] = await db.Cities.AsNoTracking().Where(c => c.DaneCode == dane).Select(c => c.Name).FirstOrDefaultAsync(ct);

        campos[C.Fecha] = FormatoDeFirma.Fecha(hoy);
        campos[C.FechaLarga] = FormatoDeFirma.FechaLarga(hoy);
        campos[C.FechaDia] = hoy.Day.ToString(CultureInfo.InvariantCulture);
        campos[C.FechaDiaLetras] = ValorEnLetras.Entero(hoy.Day);
        campos[C.FechaMes] = FormatoDeFirma.Mes(hoy);
        campos[C.FechaAnio] = hoy.Year.ToString(CultureInfo.InvariantCulture);

        var linea = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == s.CreditLineId, ct);
        var monto = s.ApprovedAmount ?? s.RequestedAmount;
        var cuotas = DecisionConditionsInput.DeJson(s.ApprovedTerms)?.Installments ?? s.Installments;
        campos[C.SolicitudNumero] = s.Number;
        campos[C.LineaCodigo] = linea?.Code;
        campos[C.LineaNombre] = linea?.Name;
        campos[C.Destino] = s.Purpose;
        campos[C.Monto] = FormatoDeFirma.Pesos(monto);
        campos[C.MontoLetras] = FormatoDeFirma.Letras(monto);
        campos[C.PlazoMeses] = s.TermMonths?.ToString(CultureInfo.InvariantCulture);
        campos[C.NumeroCuotas] = cuotas?.ToString(CultureInfo.InvariantCulture);
        campos[C.Periodicidad] = FormatoDeFirma.Periodicidad(s.Periodicity);
        campos[C.SistemaAmortizacion] = FormatoDeFirma.Sistema(s.AmortizationSystem);
        campos[C.CanalDePago] = FormatoDeFirma.Canal(s.RepaymentChannel);
        campos[C.EsLibranza] = s.RepaymentChannel is RepaymentChannel.OwnPayroll or RepaymentChannel.PaymasterDeduction ? "SI" : "NO";
        campos[C.PagareNumero] = numeroDePagare;

        // Las personas.
        var partes = await db.CreditApplicationParties.AsNoTracking()
            .Where(p => p.ApplicationId == s.Id && !p.IsDeleted && p.Role == CreditPartyRole.CoDebtor && p.PersonId != s.ApplicantPersonId)
            .OrderBy(p => p.Order).Select(p => p.PersonId).ToListAsync(ct);
        var ids = partes.Prepend(s.ApplicantPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var ciudades = personas.Values.Where(p => p.CityId != null).Select(p => p.CityId!.Value).Distinct().ToList();
        var nombresDeCiudad = await db.Cities.AsNoTracking().Where(c => ciudades.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        string? Ciudad(Person p) => p.CityId is { } id && nombresDeCiudad.TryGetValue(id, out var n) ? n : null;

        var deudor = personas[s.ApplicantPersonId];
        var tipoDelDeudor = TiposDeDocumento.PorCodigoDePersona(deudor.IdType);
        campos[C.DeudorNombre] = ReferenciasDeGobierno.Nombre(deudor);
        campos[C.DeudorTipoDocumento] = Sigla(deudor.IdType);
        campos[C.DeudorTipoDocumentoNombre] = tipoDelDeudor?.Nombre.ToLower(CultureInfo.GetCultureInfo("es-CO"));
        campos[C.DeudorDocumento] = FormatoDeFirma.Documento(deudor.TaxId);
        campos[C.DeudorExpedicion] = deudor.IdIssuedAt;
        campos[C.DeudorDireccion] = deudor.Address;
        campos[C.DeudorCiudad] = Ciudad(deudor);
        campos[C.DeudorTelefono] = deudor.Mobile ?? deudor.Phone1 ?? deudor.Phone2;
        campos[C.DeudorCorreo] = deudor.Email;

        var codeudores = partes.Where(personas.ContainsKey).Select(id => personas[id]).ToList();
        campos[C.HayCodeudores] = codeudores.Count > 0 ? "SI" : "NO";
        campos[C.CodeudoresNombres] = codeudores.Count == 0 ? null : string.Join(", ", codeudores.Select(ReferenciasDeGobierno.Nombre));
        var listaDeCodeudores = codeudores.Select(p => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [C.CodeudorNombre] = ReferenciasDeGobierno.Nombre(p),
            [C.CodeudorTipoDocumento] = Sigla(p.IdType),
            [C.CodeudorDocumento] = FormatoDeFirma.Documento(p.TaxId),
            [C.CodeudorExpedicion] = p.IdIssuedAt,
            [C.CodeudorDireccion] = p.Address,
            [C.CodeudorCiudad] = Ciudad(p),
            [C.CodeudorTelefono] = p.Mobile ?? p.Phone1 ?? p.Phone2,
        }).ToList();

        var firmanteDeudor = new FirmanteParaImprimir(campos[C.DeudorNombre]!, campos[C.DeudorTipoDocumento]!, campos[C.DeudorDocumento]!, "Deudor");
        var firmantesCodeudores = codeudores.Select(p => new FirmanteParaImprimir(ReferenciasDeGobierno.Nombre(p), Sigla(p.IdType),
            FormatoDeFirma.Documento(p.TaxId), "Codeudor")).ToList();
        var listaDeFirmantes = firmantesCodeudores.Prepend(firmanteDeudor).Select(f => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [C.FirmanteNombre] = f.Nombre, [C.FirmanteTipoDocumento] = f.TipoDeDocumento, [C.FirmanteDocumento] = f.Documento, [C.FirmanteCalidad] = f.Calidad,
        }).ToList();

        // La pagaduría: la de la libranza, o la cooperativa si descuenta su propia nómina.
        if (s.RepaymentChannel == RepaymentChannel.OwnPayroll)
        {
            campos[C.PagaduriaNombre] = cooperativa;
            campos[C.PagaduriaNit] = nit;
        }
        else if (s.PayrollAgreementId is { } convenio)
        {
            var pagador = await (from a in db.PayrollAgreements.AsNoTracking()
                                 join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.PaymasterPersonId equals p.Id
                                 where a.Id == convenio
                                 select p).FirstOrDefaultAsync(ct);
            if (pagador is not null)
            {
                campos[C.PagaduriaNombre] = ReferenciasDeGobierno.Nombre(pagador);
                campos[C.PagaduriaNit] = string.IsNullOrWhiteSpace(pagador.TaxIdCheckDigit) ? pagador.TaxId : $"{pagador.TaxId}-{pagador.TaxIdCheckDigit}";
            }
        }

        campos[C.PagaduriaCodigoEmpleado] = s.PayrollEmployeeIdentifier;

        // La cuota, las tasas y las fechas: la simulación con lo aprobado, como la hace el desembolso.
        if (linea is not null && cuotas is > 0)
        {
            try
            {
                var fecha = s.ExpectedDisbursementDate is { } prevista && prevista >= hoy ? prevista : hoy;
                campos[C.FechaDesembolso] = FormatoDeFirma.Fecha(fecha);
                var perfil = await PersonaDeOriginacion.PerfilAsync(db, deudor, fecha, ct);
                var gracia = s.GraceKind is { } g ? new ScheduleGraceInput(g, s.GraceInstallments ?? 0) : null;
                var extraordinarias = s.ExtraordinaryInstallments is { } x ? JsonSerializer.Deserialize<List<ScheduleExtraordinaryInput>>(x, Json) : null;
                var seguros = s.SelectedInsurances is { } y ? JsonSerializer.Deserialize<List<string>>(y) : null;
                var primerVencimiento = s.FirstDueDate is { } v && v > fecha ? v : (DateOnly?)null;
                var entrada = new ScheduleSimulationInput(linea.PublicId, monto, cuotas.Value, s.Periodicity, fecha, primerVencimiento, s.AmortizationSystem,
                    extraordinarias, gracia, seguros, deudor.PublicId);
                var simulada = await simulador.SimularAsync(linea, entrada, perfil, fecha, ct);
                if (simulada.IsSuccess)
                {
                    var plan = simulada.Value.Plan;
                    var cuota = plan.Cuotas.FirstOrDefault(c => !c.SoloInteres && c.Capital > 0m) ?? plan.Cuotas.FirstOrDefault();
                    if (cuota is not null)
                    {
                        campos[C.ValorCuota] = FormatoDeFirma.Pesos(cuota.Total);
                        campos[C.ValorCuotaLetras] = FormatoDeFirma.Letras(cuota.Total);
                    }

                    campos[C.TasaEfectivaAnual] = FormatoDeFirma.Porcentaje(simulada.Value.TasaPactada);
                    // 2026-11-24 (feature 025, HU2; FR-012): pactada en MV, la mensual es la pactada (exacta); si no, la equivalente de la E.A.
                    var mensual = simulada.Value.Entrada.TasaMensual
                        ?? (decimal)(Math.Pow(1d + (double)simulada.Value.TasaPactada, 1d / 12d) - 1d);
                    campos[C.TasaMensual] = FormatoDeFirma.Porcentaje(mensual);
                    if (plan.Cuotas.Count > 0)
                    {
                        campos[C.FechaPrimeraCuota] = FormatoDeFirma.Fecha(plan.Cuotas[0].Vencimiento);
                        campos[C.FechaUltimaCuota] = FormatoDeFirma.Fecha(plan.Cuotas[^1].Vencimiento);
                    }
                }
                else
                {
                    avisos.Add($"No se pudo calcular la cuota ni la tasa: {simulada.Error.Message}");
                }
            }
            catch (JsonException)
            {
                avisos.Add("No se pudo leer el detalle de la solicitud para calcular la cuota.");
            }
        }

        foreach (var (codigo, valor) in propios) campos[codigo] = valor;

        var listas = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string?>>>(StringComparer.Ordinal)
        {
            [C.Codeudores] = listaDeCodeudores,
            [C.Firmantes] = listaDeFirmantes,
        };
        return new DatosDeLaSolicitud(new DatosDePlantilla(campos, listas), firmanteDeudor, firmantesCodeudores, linea?.Code, avisos);
    }

    /// <summary>La sigla con que se escribe el documento («C.C.», «C.E.», «NIT»…).</summary>
    public static string Sigla(string? idType) => TiposDeDocumento.PorCodigoDePersona(idType)?.Codigo switch
    {
        "CC" or null => "C.C.",
        "CE" => "C.E.",
        "TI" => "T.I.",
        "PA" => "Pasaporte",
        "RC" => "R.C.",
        var otro => otro,
    };
}
